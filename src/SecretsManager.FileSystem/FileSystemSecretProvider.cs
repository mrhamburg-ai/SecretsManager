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
    private readonly ISecretEncryptor _encryptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The filesystem options.</param>
    public FileSystemSecretProvider(FileSystemOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = new SecretFileStore(options.BasePath);
        _encryptor = CreateEncryptor(options);
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
            var value = DecryptValue(envelope);

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

            if (_encryptor.IsEnabled)
            {
                var payload = _encryptor.Encrypt(Encoding.UTF8.GetBytes(value));
                envelope.Encrypted = true;
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
    public ValueTask DisposeAsync()
    {
        _encryptor.Dispose();
        _store.Dispose();
        return ValueTask.CompletedTask;
    }

    private string DecryptValue(VersionedSecretEnvelope envelope)
    {
        if (!envelope.Encrypted)
            return envelope.Value ?? "";

        var payload = new EncryptedPayload(
            Convert.FromBase64String(envelope.Nonce ?? ""),
            Convert.FromBase64String(envelope.Ciphertext ?? ""),
            Convert.FromBase64String(envelope.AuthTag ?? ""));

        return Encoding.UTF8.GetString(_encryptor.Decrypt(payload));
    }

    private static ISecretEncryptor CreateEncryptor(FileSystemOptions options)
    {
        if (options.EncryptionKey is { } base64Key)
            return new AesGcmEncryptor(Convert.FromBase64String(base64Key));

        if (options.EncryptionKeyFile is { } keyFilePath)
        {
            var raw = File.ReadAllBytes(keyFilePath);
            // If the file is exactly 32 bytes, use as-is; otherwise treat as base64
            var key = raw.Length == 32 ? raw : Convert.FromBase64String(Encoding.UTF8.GetString(raw).Trim());
            return new AesGcmEncryptor(key);
        }

        return new NullEncryptor();
    }
}
