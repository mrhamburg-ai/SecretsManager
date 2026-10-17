using System.Text;
using System.Text.Json;
using SecretsManager.Internal;
using SecretsManager.PostgreSql.Internal;

namespace SecretsManager.PostgreSql;

/// <summary>
/// An <see cref="ISecretProvider"/> backed by a PostgreSQL database.
/// Secrets are stored in dedicated tables with optional AES-256-GCM encryption at rest.
/// </summary>
public sealed class PostgreSqlSecretProvider : ISecretProvider
{
    private readonly IPostgreSqlSecretStore _store;
    private readonly ISecretEncryptor _encryptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The PostgreSQL connection and behavior options.</param>
    public PostgreSqlSecretProvider(PostgreSqlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Invalid {nameof(PostgreSqlOptions)}: {string.Join("; ", errors)}", nameof(options));

        _store = new PostgreSqlSecretStore(options);
        _encryptor = CreateEncryptor(options);
    }

    internal PostgreSqlSecretProvider(IPostgreSqlSecretStore store, ISecretEncryptor encryptor)
    {
        _store = store;
        _encryptor = encryptor;
    }

    /// <summary>
    /// Retrieves a secret by key from the PostgreSQL store.
    /// When <paramref name="query"/> specifies a version, that version is returned;
    /// otherwise the latest version is returned. When <paramref name="query"/> specifies
    /// a property, only that property is extracted from the (JSON) value.
    /// </summary>
    /// <param name="key">The secret key to look up.</param>
    /// <param name="query">Optional version or property filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The secret value with metadata.</returns>
    /// <exception cref="SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="SecretProviderException">A PostgreSQL error occurred.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _store.GetSecretByKeyAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            int? versionNumber = null;
            if (query?.Version is { } versionStr)
            {
                if (!int.TryParse(versionStr, out var parsed))
                    throw new SecretProviderException(
                        $"Invalid version '{versionStr}' for secret '{key}': version must be a positive integer.");
                versionNumber = parsed;
            }

            var version = await _store.GetVersionAsync(secret.Id, versionNumber, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var value = DecryptValue(key, version);

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = version.Version.ToString(),
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt,
                ContentType = secret.ContentType,
                Tags = secret.Tags
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"PostgreSQL error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in the PostgreSQL store.
    /// If the secret does not exist it is created; otherwise a new version is appended.
    /// The value is encrypted at rest when an encryption key is configured.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional content type and tags.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored secret value with the new version information.</returns>
    /// <exception cref="SecretProviderException">A PostgreSQL error occurred.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var storedValue = EncryptValue(value, out var encrypted);

            var (secret, version) = await _store.PutSecretAsync(
                key, storedValue, encrypted,
                metadata?.ContentType, metadata?.Tags, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = version.Version.ToString(),
                CreatedAt = version.CreatedAt,
                UpdatedAt = secret.UpdatedAt,
                ContentType = secret.ContentType,
                Tags = secret.Tags
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"PostgreSQL error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret and all its versions from the PostgreSQL store.
    /// </summary>
    /// <param name="key">The secret key to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="SecretProviderException">A PostgreSQL error occurred.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _store.DeleteSecretAsync(key, cancellationToken);
            if (!deleted)
                throw new SecretNotFoundException(key);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"PostgreSQL error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret stored in PostgreSQL.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of version information.</returns>
    /// <exception cref="SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="SecretProviderException">A PostgreSQL error occurred.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _store.GetSecretByKeyAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var versions = await _store.ListVersionsAsync(secret.Id, cancellationToken);

            return versions
                .Select(v => new SecretVersionInfo
                {
                    Version = v.Version.ToString(),
                    CreatedAt = v.CreatedAt,
                    IsCurrent = v.IsCurrent
                })
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"PostgreSQL error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in the PostgreSQL store without retrieving its value.
    /// </summary>
    /// <param name="key">The secret key to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">A PostgreSQL error occurred.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _store.SecretExistsAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new SecretProviderException(
                $"PostgreSQL error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying PostgreSQL store and encryptor resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _encryptor.Dispose();
        await _store.DisposeAsync();
    }

    private string EncryptValue(string value, out bool encrypted)
    {
        if (!_encryptor.IsEnabled)
        {
            encrypted = false;
            return value;
        }

        var payload = _encryptor.Encrypt(Encoding.UTF8.GetBytes(value));
        encrypted = true;
        return JsonSerializer.Serialize(new
        {
            n = Convert.ToBase64String(payload.Nonce),
            c = Convert.ToBase64String(payload.Ciphertext),
            t = Convert.ToBase64String(payload.Tag)
        });
    }

    private string DecryptValue(string key, PostgreSqlVersionRow version)
    {
        // The point of encrypting at rest is that someone with database access cannot read or tamper
        // with secrets. A row marked unencrypted while a key is configured is exactly what
        // UPDATE ... SET encrypted = false, value = 'attacker-value' produces.
        if (_encryptor.IsEnabled && !version.Encrypted)
            throw new SecretProviderException(
                $"Secret '{key}' version {version.Version} is stored unencrypted, but an encryption " +
                "key is configured. Refusing to return a value that may have been tampered with.");

        if (!_encryptor.IsEnabled && version.Encrypted)
            throw new SecretProviderException(
                $"Secret '{key}' version {version.Version} is encrypted, but no encryption key is configured.");

        if (!version.Encrypted)
            return version.Value;

        using var doc = JsonDocument.Parse(version.Value);
        var root = doc.RootElement;

        var payload = new EncryptedPayload(
            Convert.FromBase64String(root.GetProperty("n").GetString()!),
            Convert.FromBase64String(root.GetProperty("c").GetString()!),
            Convert.FromBase64String(root.GetProperty("t").GetString()!));

        return Encoding.UTF8.GetString(_encryptor.Decrypt(payload));
    }

    private static ISecretEncryptor CreateEncryptor(PostgreSqlOptions options)
    {
        if (options.EncryptionKey is { } base64Key)
            return new AesGcmEncryptor(Convert.FromBase64String(base64Key));

        if (options.EncryptionKeyFile is { } keyFilePath)
        {
            var raw = File.ReadAllBytes(keyFilePath);
            var key = raw.Length == 32 ? raw : Convert.FromBase64String(Encoding.UTF8.GetString(raw).Trim());
            return new AesGcmEncryptor(key);
        }

        return new NullEncryptor();
    }
}
