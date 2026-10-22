using SecretsManager.Internal;
using SecretsManager.IBMCloudSecretsManager.Internal;

namespace SecretsManager.IBMCloudSecretsManager;

/// <summary>
/// An <see cref="ISecretProvider"/> implementation that stores and retrieves secrets using IBM Cloud Secrets Manager.
/// </summary>
public sealed class IBMCloudSecretsManagerSecretProvider : ISecretProvider
{
    private readonly IIBMCloudSecretsManagerApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="IBMCloudSecretsManagerSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The options containing the IBM Cloud region, instance ID, and API key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required option value is missing or empty.</exception>
    public IBMCloudSecretsManagerSecretProvider(IBMCloudSecretsManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Region))
            throw new ArgumentException("Region is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.InstanceId))
            throw new ArgumentException("InstanceId is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _client = new IBMCloudSecretsManagerApiClient(options);
    }

    internal IBMCloudSecretsManagerSecretProvider(IIBMCloudSecretsManagerApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret by name from IBM Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The name of the secret to retrieve.</param>
    /// <param name="query">Optional query parameters to select a specific version or extract a property.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The secret value with metadata.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing IBM Cloud Secrets Manager.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);
            var revision = query?.Version ?? "latest";
            var access = await _client.AccessVersionAsync(secret.Id, revision, cancellationToken);
            var value = access.Data;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = access.Revision.ToString(),
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"IBM Cloud Secrets Manager error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in IBM Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The name of the secret.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata including tags to apply when creating the secret.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored secret value with version information.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing IBM Cloud Secrets Manager.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken);

            if (secret is null)
            {
                var tags = metadata?.Tags?.Keys.ToArray();
                secret = await _client.CreateSecretAsync(key, tags, cancellationToken);
            }

            var version = await _client.CreateVersionAsync(secret.Id, value, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = version.Revision.ToString(),
                CreatedAt = version.CreatedAt,
                UpdatedAt = version.UpdatedAt
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"IBM Cloud Secrets Manager error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from IBM Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The name of the secret to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing IBM Cloud Secrets Manager.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            await _client.DeleteSecretAsync(secret.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"IBM Cloud Secrets Manager error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in IBM Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The name of the secret.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of version information.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing IBM Cloud Secrets Manager.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var versions = await _client.ListVersionsAsync(secret.Id, cancellationToken);

            return versions
                .Select(v => new SecretVersionInfo
                {
                    Version = v.Revision.ToString(),
                    CreatedAt = v.CreatedAt,
                    IsCurrent = v.Latest
                })
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"IBM Cloud Secrets Manager error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in IBM Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The name of the secret to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing IBM Cloud Secrets Manager.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken);
            return secret is not null;
        }
        catch (Exception ex)
        {
            throw new SecretProviderException(
                $"IBM Cloud Secrets Manager error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying API client and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
