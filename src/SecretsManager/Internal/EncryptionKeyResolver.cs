using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SecretsManager;

namespace SecretsManager.Internal;

/// <summary>
/// The resolved key for new writes, plus the identifier of the key version that produced it.
/// </summary>
/// <remarks>
/// A reference type on purpose: it is cached in a single field that is read without the lock on the
/// fast path, and a multi-word value type read concurrently with a write can tear, pairing one key's
/// encryptor with another key's id — which silently produces ciphertext that can never be decrypted.
/// </remarks>
internal sealed class EncryptionKeySlot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionKeySlot"/> class.
    /// </summary>
    /// <param name="key">The cached key and its encryptor.</param>
    /// <param name="keyId">The stable key id recorded alongside the ciphertext.</param>
    public EncryptionKeySlot(CachedKey key, string? keyId)
    {
        Key = key;
        KeyId = keyId;
    }

    /// <summary>Gets the cached key and its encryptor.</summary>
    public CachedKey Key { get; }

    /// <summary>Gets the stable key id recorded alongside the ciphertext.</summary>
    public string? KeyId { get; }
}

/// <summary>
/// A cached encryptor and the key bytes it was built from, so a key id presented again with
/// different bytes can be detected instead of silently serving the wrong key.
/// </summary>
internal sealed class CachedKey
{
    public CachedKey(byte[] key, AesGcmEncryptor encryptor)
    {
        Key = key;
        Encryptor = encryptor;
    }

    public byte[] Key { get; }

    public AesGcmEncryptor Encryptor { get; }
}

/// <summary>
/// Resolves the encryption key for a provider from a static key, a key file, or an
/// <see cref="IEncryptionKeyProvider"/>.
/// </summary>
/// <remarks>
/// Resolution is lazy — the constructor performs no I/O, so a key held in another provider can be
/// fetched over the network — and cached per key version. It never falls back to plaintext: a key
/// that cannot be resolved throws <see cref="SecretProviderException"/> rather than silently
/// disabling encryption.
/// </remarks>
public sealed class EncryptionKeyResolver : IAsyncDisposable
{
    private readonly string? _base64Key;
    private readonly string? _keyFilePath;
    private readonly IEncryptionKeyProvider? _provider;
    private readonly string? _legacyBase64Key;

    private readonly SemaphoreSlim _lock = new(1, 1);

    // A ConcurrentDictionary so the cache can be probed without taking the lock: a read of already
    // resolved ciphertext then never waits behind an in-flight network fetch for a different key.
    private readonly ConcurrentDictionary<string, CachedKey> _encryptors =
        new(StringComparer.Ordinal);

    private EncryptionKeySlot? _current;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionKeyResolver"/> class.
    /// </summary>
    /// <param name="base64Key">Base64-encoded key, or null.</param>
    /// <param name="keyFilePath">Path to a key file, or null.</param>
    /// <param name="provider">External key source, or null.</param>
    /// <param name="legacyBase64Key">Key used only to decrypt values written before key ids existed, or null.</param>
    public EncryptionKeyResolver(
        string? base64Key,
        string? keyFilePath,
        IEncryptionKeyProvider? provider,
        string? legacyBase64Key)
    {
        _base64Key = base64Key;
        _keyFilePath = keyFilePath;
        _provider = provider;
        _legacyBase64Key = legacyBase64Key;
    }

    /// <summary>
    /// Whether any key source is configured. When false, values are stored unencrypted and the
    /// providers refuse to read anything that claims to be encrypted.
    /// </summary>
    public bool IsEnabled =>
        _provider is not null || _base64Key is not null || _keyFilePath is not null;

    /// <summary>
    /// Resolves the encryptor for new writes, together with the key id to record alongside the
    /// ciphertext. Returns <see cref="NullEncryptor"/> with a null key id when no key is configured.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The encryptor and the key id, if any.</returns>
    /// <exception cref="ObjectDisposedException">The resolver has been disposed.</exception>
    public async ValueTask<(ISecretEncryptor Encryptor, string? KeyId)> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return (new NullEncryptor(), null);

        if (_disposed)
            throw new ObjectDisposedException(nameof(EncryptionKeyResolver));

        // Fast path, no lock: the slot is a reference, so the pair is published atomically.
        if (Volatile.Read(ref _current) is { } cached)
            return (cached.Key.Encryptor, cached.KeyId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Resolve into a local, then publish. InvalidateKey clears the field without the lock,
            // so it can be cleared again between the check and the read; resolving first means the
            // returned pair always comes from a slot this call itself created.
            var resolved = _current;
            if (resolved is null)
            {
                if (_provider is not null)
                {
                    var material = await _provider.GetCurrentKeyAsync(cancellationToken).ConfigureAwait(false);
                    resolved = new EncryptionKeySlot(
                        GetOrCreateEncryptor(material.KeyId, material.Key), material.KeyId);
                }
                else
                {
                    resolved = new EncryptionKeySlot(
                        GetOrCreateEncryptor(string.Empty, ReadStaticKey()), null);
                }

                Volatile.Write(ref _current, resolved);
            }

            return (resolved.Key.Encryptor, resolved.KeyId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Resolves the encryptor that produced a given key id, for reading existing ciphertext.
    /// A null key id means the value predates key ids: it is read with
    /// <c>LegacyEncryptionKey</c> when set, otherwise with the static key.
    /// </summary>
    /// <param name="keyId">The key id recorded alongside the ciphertext, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The encryptor for that key.</returns>
    /// <exception cref="SecretProviderException">No key is available for the given id.</exception>
    public async ValueTask<ISecretEncryptor> GetByKeyIdAsync(
        string? keyId, CancellationToken cancellationToken = default)
    {
        if (keyId is null)
        {
            var legacy = _legacyBase64Key ?? _base64Key;
            if (legacy is not null)
                return GetOrCreateEncryptor(string.Empty, Convert.FromBase64String(legacy)).Encryptor;

            if (_keyFilePath is not null)
                return GetOrCreateEncryptor(string.Empty, ReadStaticKey()).Encryptor;

            throw new SecretProviderException(
                "This value was written without a key id and no LegacyEncryptionKey is configured, " +
                "so the key that encrypted it cannot be resolved.");
        }

        if (_provider is null)
            throw new SecretProviderException(
                $"This value records key id '{keyId}', but no EncryptionKeyProvider is configured to resolve it.");

        // Lock-free read of the cache first: this is the hot path for reading already-resolved values.
        if (_encryptors.TryGetValue(keyId, out var cached))
            return cached.Encryptor;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_encryptors.TryGetValue(keyId, out cached))
                return cached.Encryptor;

            var material = await _provider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
            return GetOrCreateEncryptor(keyId, material.Key).Encryptor;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Drops the cached current key so the next write re-reads it from the source. Cached historical
    /// keys are kept, because existing ciphertext still needs them.
    /// </summary>
    /// <remarks>
    /// Does not take the lock: a synchronous wait would block behind any in-flight fetch, which is
    /// exactly what the async dispose path exists to avoid. The cache is only written under the lock
    /// by <see cref="GetCurrentAsync"/>, and this clear is idempotent.
    /// </remarks>
    public void InvalidateKey()
    {
        Volatile.Write(ref _current, null);
    }

    /// <summary>
    /// Releases the cached encryptors and the lock. Safe to call while a key fetch is in flight,
    /// and safe to call more than once.
    /// </summary>
    /// <remarks>
    /// Deliberately does not take the lock, and deliberately does not free the cached encryptors.
    /// Freeing them would strand any caller already holding one: <see cref="GetCurrentAsync"/>
    /// hands the encryptor to its caller, which may still be using it, so its next
    /// <c>Encrypt</c> would throw <see cref="ObjectDisposedException"/>. The lock is not taken
    /// because a synchronous wait would block behind an in-flight fetch — exactly what the async
    /// dispose path exists to avoid. The managed objects are reclaimed with the resolver.
    /// </remarks>
    public void Dispose()
    {
        _disposed = true;

        Volatile.Write(ref _current, null);
    }

    /// <summary>
    /// Stops this resolver from being used again and releases the cached encryptors.
    /// Safe to call more than once, and safe to call while a fetch is in flight.
    /// </summary>
    /// <remarks>
    /// Neither the lock nor the cached encryptors are freed. Freeing the lock would make the
    /// in-flight fetch's own <c>Release</c> throw <see cref="ObjectDisposedException"/>; freeing an
    /// encryptor would strand a caller that already took it from <see cref="GetCurrentAsync"/>.
    /// Both are safe to leave alive because the resolver is unreachable once disposed — they are
    /// reclaimed with it — and every new caller is rejected by the <c>_disposed</c> check in the
    /// getters before it can touch either.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        _disposed = true;

        Volatile.Write(ref _current, null);

        return ValueTask.CompletedTask;
    }

    private byte[] ReadStaticKey()
    {
        if (_base64Key is { } base64Key)
            return Convert.FromBase64String(base64Key);

        if (_keyFilePath is { } path)
        {
            var raw = File.ReadAllBytes(path);
            // If the file is exactly 32 bytes, use as-is; otherwise treat as base64.
            return raw.Length == 32
                ? raw
                : Convert.FromBase64String(Encoding.UTF8.GetString(raw).Trim());
        }

        throw new SecretProviderException("No encryption key is configured.");
    }

    private CachedKey GetOrCreateEncryptor(string keyId, byte[] key)
    {
        if (key.Length != 32)
            throw new SecretProviderException(
                $"Resolved key '{keyId}' is {key.Length} bytes; AES-256-GCM requires 32.");

        if (_encryptors.TryGetValue(keyId, out var existing))
        {
            // A key id must identify exactly one key. A source that hands out the same id for
            // different bytes — Passbolt always reports version "1", and any provider without
            // immutable versions behaves the same way — would otherwise make previously written
            // values permanently unreadable, with no error at the point of rotation.
            if (!CryptographicOperations.FixedTimeEquals(existing.Key, key))
                throw new SecretProviderException(
                    $"Key id '{keyId}' was already resolved to a different key. The key source must " +
                    "report a distinct version for every key it serves, or existing values encrypted " +
                    "under this id cannot be read back.");

            return existing;
        }

        var created = new CachedKey(key, new AesGcmEncryptor(key));
        return _encryptors.GetOrAdd(keyId, created);
    }
}
