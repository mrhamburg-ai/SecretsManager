using System.Text;
using SecretsManager.FileSystem.Internal;
using SecretsManager.Internal;

namespace SecretsManager.FileSystem;

/// <summary>
/// Stores and retrieves secrets from the local filesystem with optional AES-256-GCM encryption.
/// Supports versioning, metadata, tags, and JSON property extraction.
/// </summary>
public sealed class FileSystemSecretProvider : ISecretProvider
{
    private readonly SecretFileStore _store;
    private readonly EncryptionKeyResolver _keys;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The filesystem options.</param>
    public FileSystemSecretProvider(FileSystemOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Invalid {nameof(FileSystemOptions)}: {string.Join("; ", errors)}", nameof(options));

        _store = new SecretFileStore(options.BasePath);
        _keys = new EncryptionKeyResolver(
            options.EncryptionKey,
            options.EncryptionKeyFile,
            options.EncryptionKeyProvider);
    }

    /// <summary>
    /// Reads a secret from the local filesystem, optionally extracting a JSON property.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="query">The secret query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        var semaphore = _store.GetLock(key);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var version = query?.Version
                ?? await _store.ReadCurrentVersionAsync(key, cancellationToken);

            var envelope = await _store.ReadEnvelopeAsync(key, version, cancellationToken);
            var value = await DecryptValueAsync(key, envelope, cancellationToken);

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = envelope.Version,
                CreatedAt = envelope.CreatedAt,
                ContentType = envelope.ContentType,
                Tags = envelope.Tags?.AsReadOnly()
            };
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Writes a secret to the local filesystem with optional AES-256-GCM encryption.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="value">The secret value.</param>
    /// <param name="metadata">The secret metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value.</returns>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default)
    {
        var semaphore = _store.GetLock(key);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var version = _store.GetNextVersion(key);
            var now = DateTimeOffset.UtcNow;

            var envelope = new VersionedSecretEnvelope
            {
                Version = version,
                CreatedAt = now,
                ContentType = metadata?.ContentType,
                Tags = metadata?.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            var (encryptor, keyId) = await _keys.GetCurrentAsync(cancellationToken);

            if (encryptor.IsEnabled)
            {
                // The key id, the secret key and the version are authenticated as associated data, so
                // a payload copied to a different secret, version, or key fails to decrypt rather
                // than returning the wrong value under a valid tag.
                var associatedData = Encoding.UTF8.GetBytes($"{keyId}|{key}|{version}");
                var payload = encryptor.Encrypt(Encoding.UTF8.GetBytes(value), associatedData);
                envelope.Encrypted = true;
                envelope.KeyId = keyId;
                envelope.Nonce = Convert.ToBase64String(payload.Nonce);
                envelope.Ciphertext = Convert.ToBase64String(payload.Ciphertext);
                envelope.AuthTag = Convert.ToBase64String(payload.Tag);
            }
            else
            {
                envelope.Encrypted = false;
                envelope.Value = value;
            }

            await _store.WriteEnvelopeAsync(key, envelope, cancellationToken);
            await _store.WriteCurrentVersionAsync(key, version, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = version,
                CreatedAt = now,
                ContentType = metadata?.ContentType,
                Tags = metadata?.Tags
            };
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Deletes a secret and all its versions from the local filesystem.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var semaphore = _store.GetLock(key);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_store.SecretExists(key))
                throw new SecretNotFoundException(key);

            _store.DeleteSecret(key);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Lists all versions of a secret stored on the local filesystem.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of secret versions.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var semaphore = _store.GetLock(key);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_store.SecretExists(key))
                throw new SecretNotFoundException(key);

            var currentVersion = await _store.ReadCurrentVersionAsync(key, cancellationToken);
            var versions = _store.GetVersions(key);
            var result = new List<SecretVersionInfo>();

            foreach (var version in versions)
            {
                var envelope = await _store.ReadEnvelopeAsync(key, version, cancellationToken);
                result.Add(new SecretVersionInfo
                {
                    Version = envelope.Version,
                    CreatedAt = envelope.CreatedAt,
                    IsCurrent = envelope.Version == currentVersion
                });
            }

            return result.AsReadOnly();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Checks whether a secret exists on the local filesystem.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.SecretExists(key));
    }

    /// <summary>
    /// Releases the underlying file store and encryptor resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _keys.DisposeAsync().ConfigureAwait(false);
        _store.Dispose();
    }

    /// <summary>
    /// Resolves the encryption key before the first operation, so a misconfigured key source fails
    /// at startup rather than on the first read or write. Optional — without it the key is resolved
    /// lazily on first use.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async ValueTask WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await _keys.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the cached encryption key so the next write re-reads it from the configured key source.
    /// Cached historical keys are kept, because existing ciphertext still needs them.
    /// </summary>
    public void InvalidateKey() => _keys.InvalidateKey();

    private async ValueTask<string> DecryptValueAsync(
        string key, VersionedSecretEnvelope envelope, CancellationToken cancellationToken)
    {
        // An envelope that is not encrypted while a key is configured has either been tampered with
        // or written by a misconfigured process. Returning it would let anyone with write access to
        // the store plant an attacker-chosen value, which is the whole point of authenticated
        // encryption being lost.
        if (_keys.IsEnabled && !envelope.Encrypted)
            throw new SecretProviderException(
                $"Secret '{key}' version '{envelope.Version}' is stored unencrypted, but an " +
                "encryption key is configured. Refusing to return a value that may have been tampered with.");

        if (!_keys.IsEnabled && envelope.Encrypted)
            throw new SecretProviderException(
                $"Secret '{key}' version '{envelope.Version}' is encrypted, but no encryption key is configured.");

        if (!envelope.Encrypted)
            return envelope.Value ?? "";

        var encryptor = await _keys.GetByKeyIdAsync(envelope.KeyId, cancellationToken).ConfigureAwait(false);

        // Rebuild the same associated data the write path authenticated.
        var associatedData = Encoding.UTF8.GetBytes($"{envelope.KeyId}|{key}|{envelope.Version}");

        var payload = new EncryptedPayload(
            Convert.FromBase64String(envelope.Nonce ?? ""),
            Convert.FromBase64String(envelope.Ciphertext ?? ""),
            Convert.FromBase64String(envelope.AuthTag ?? ""),
            associatedData);

        return Encoding.UTF8.GetString(encryptor.Decrypt(payload));
    }
}
