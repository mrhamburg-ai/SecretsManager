namespace SecretsManager;

/// <summary>
/// Unified interface for secret management backends.
/// Each provider (Azure Key Vault, HashiCorp Vault, OpenStack Barbican, etc.)
/// implements this interface to expose a consistent API for secret operations.
/// </summary>
public interface ISecretProvider : IAsyncDisposable
{
    /// <summary>
    /// Retrieves a secret by key, optionally targeting a specific version or property.
    /// </summary>
    Task<SecretValue> GetSecretAsync(
        string key,
        SecretQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a secret.
    /// </summary>
    Task<SecretValue> PutSecretAsync(
        string key,
        string value,
        SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret by key.
    /// </summary>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    Task DeleteSecretAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available versions of a secret.
    /// </summary>
    Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a secret exists without retrieving its value.
    /// </summary>
    Task<bool> SecretExistsAsync(
        string key,
        CancellationToken cancellationToken = default);
}
