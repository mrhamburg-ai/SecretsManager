using System.Text;

namespace SecretsManager.Internal;

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
    private readonly Dictionary<string, AesGcmEncryptor> _encryptors = new(StringComparer.Ordinal);
    private bool _disposed;

    private (AesGcmEncryptor Encryptor, string? KeyId)? _current;

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
    public async ValueTask<(ISecretEncryptor Encryptor, string? KeyId)> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return (new NullEncryptor(), null);

        if (_current is { } cached)
            return (cached.Encryptor, cached.KeyId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Assigned inside the lock rather than caching a Task, so a failed or cancelled first
            // fetch is not stored and the next call retries.
            if (_current is null)
            {
                if (_provider is not null)
                {
                    var material = await _provider.GetCurrentKeyAsync(cancellationToken).ConfigureAwait(false);
                    _current = (GetOrCreateEncryptor(material.KeyId, material.Key), material.KeyId);
                }
                else
                {
                    _current = (GetOrCreateEncryptor(string.Empty, ReadStaticKey()), null);
                }
            }

            return (_current.Value.Encryptor, _current.Value.KeyId);
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
                return GetOrCreateEncryptor(string.Empty, Convert.FromBase64String(legacy));

            if (_keyFilePath is not null)
                return GetOrCreateEncryptor(string.Empty, ReadStaticKey());

            throw new SecretProviderException(
                "This value was written without a key id and no LegacyEncryptionKey is configured, " +
                "so the key that encrypted it cannot be resolved.");
        }

        if (_provider is null)
            throw new SecretProviderException(
                $"This value records key id '{keyId}', but no EncryptionKeyProvider is configured to resolve it.");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_encryptors.TryGetValue(keyId, out var cached))
                return cached;

            var material = await _provider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
            return GetOrCreateEncryptor(keyId, material.Key);
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
    public void InvalidateKey()
    {
        _lock.Wait();
        try
        {
            _current = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Releases the cached encryptors and the lock. Safe to call while a key fetch is in flight:
    /// it waits for the lock first, so it never disposes the semaphore under a running fetch.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Releases the cached encryptors and the lock, waiting for any in-flight key fetch to finish
    /// before freeing the lock. Async, because the wait can be indefinite: a key source that blocks
    /// holds the lock until it returns, and a synchronous <c>Dispose</c> would block the caller.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var encryptor in _encryptors.Values)
                encryptor.Dispose();

            _encryptors.Clear();
        }
        finally
        {
            _lock.Release();
        }

        _lock.Dispose();
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

    private AesGcmEncryptor GetOrCreateEncryptor(string keyId, byte[] key)
    {
        if (key.Length != 32)
            throw new SecretProviderException(
                $"Resolved key '{keyId}' is {key.Length} bytes; AES-256-GCM requires 32.");

        if (_encryptors.TryGetValue(keyId, out var existing))
            return existing;

        var encryptor = new AesGcmEncryptor(key);
        _encryptors[keyId] = encryptor;
        return encryptor;
    }
}
