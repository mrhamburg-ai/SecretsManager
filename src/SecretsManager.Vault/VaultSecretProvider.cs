using SecretsManager.Internal;
using SecretsManager.Vault.Internal;

namespace SecretsManager.Vault;

/// <summary>
/// Secret provider that stores and retrieves secrets using a HashiCorp Vault server.
/// </summary>
public sealed class VaultSecretProvider : ISecretProvider
{
    private readonly IVaultApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="VaultSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Vault configuration options.</param>
    public VaultSecretProvider(VaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Url))
            throw new ArgumentException("Url is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Token))
            throw new ArgumentException("Token is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.MountPath))
            throw new ArgumentException("MountPath is required.", nameof(options));

        _client = new VaultApiClient(options);
    }

    internal VaultSecretProvider(IVaultApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret value from HashiCorp Vault by key.
    /// </summary>
    /// <param name="key">The secret path to retrieve.</param>
    /// <param name="query">Optional query specifying a version number or property to extract.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SecretValue"/> containing the secret data.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Vault API call fails.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            int? version = null;
            if (query?.Version is { } v && int.TryParse(v, out var parsed))
                version = parsed;

            var secret = await _client.GetSecretAsync(key, version, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var value = secret.Value;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = secret.Version.ToString(),
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Vault error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in HashiCorp Vault.
    /// </summary>
    /// <param name="key">The secret path to create or update.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata (not currently used by this provider).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SecretValue"/> containing the stored secret data and version information.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Vault API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.PutSecretAsync(key, value, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.Version.ToString(),
                CreatedAt = result.CreatedAt,
                UpdatedAt = result.UpdatedAt
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Vault error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from HashiCorp Vault.
    /// </summary>
    /// <param name="key">The secret path to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretProviderException">Thrown when the Vault API call fails.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Vault error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all non-destroyed versions of a secret from HashiCorp Vault.
    /// </summary>
    /// <param name="key">The secret path to list versions for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SecretVersionInfo"/> describing each version.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Vault API call fails.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _client.ListVersionsAsync(key, cancellationToken);

            return versions
                .Where(v => !v.Destroyed)
                .Select(v => new SecretVersionInfo
                {
                    Version = v.Version.ToString(),
                    CreatedAt = v.CreatedAt,
                    IsCurrent = v.IsCurrent
                })
                .ToList()
                .AsReadOnly();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Vault error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in HashiCorp Vault.
    /// </summary>
    /// <param name="key">The secret path to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Vault API call fails.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.SecretExistsAsync(key, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new SecretProviderException(
                $"Vault error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying Vault API client and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
