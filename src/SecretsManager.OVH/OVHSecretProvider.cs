using SecretsManager.Internal;
using SecretsManager.OVH.Internal;

namespace SecretsManager.OVH;

/// <summary>
/// An <see cref="ISecretProvider"/> implementation that stores and retrieves secrets using OVHcloud Secrets Management.
/// </summary>
public sealed class OVHSecretProvider : ISecretProvider
{
    private readonly IOVHApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="OVHSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The options containing the OVH endpoint, application credentials, consumer key, and OKMS ID.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required option value is missing or empty.</exception>
    public OVHSecretProvider(OVHOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new ArgumentException("Endpoint is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApplicationKey))
            throw new ArgumentException("ApplicationKey is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApplicationSecret))
            throw new ArgumentException("ApplicationSecret is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ConsumerKey))
            throw new ArgumentException("ConsumerKey is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.OkmsId))
            throw new ArgumentException("OkmsId is required.", nameof(options));

        _client = new OVHOAuthHttpClient(options);
    }

    internal OVHSecretProvider(IOVHApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret by name from OVHcloud Secrets Management.
    /// </summary>
    /// <param name="key">The name of the secret to retrieve.</param>
    /// <param name="query">Optional query parameters to select a specific version or extract a property.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The secret value with metadata.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing OVHcloud.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var currentVersion = GetCurrentVersion(secret, query?.Version);

            var access = await _client.AccessVersionAsync(key, currentVersion, cancellationToken);
            var value = access.Data;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = access.Id.ToString(),
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"OVH error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in OVHcloud Secrets Management.
    /// </summary>
    /// <param name="key">The name of the secret.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata (not currently used by this provider).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored secret value with version information.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing OVHcloud.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _client.FindSecretByNameAsync(key, cancellationToken);

            if (existing is null)
            {
                var created = await _client.CreateSecretAsync(key, value, cancellationToken);

                return new SecretValue
                {
                    Key = key,
                    Value = value,
                    Version = "1",
                    CreatedAt = created.CreatedAt,
                    UpdatedAt = created.UpdatedAt
                };
            }
            else
            {
                var version = await _client.CreateVersionAsync(key, value, cancellationToken);

                return new SecretValue
                {
                    Key = key,
                    Value = value,
                    Version = version.Id.ToString(),
                    CreatedAt = existing.CreatedAt,
                    UpdatedAt = existing.UpdatedAt
                };
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"OVH error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from OVHcloud Secrets Management.
    /// </summary>
    /// <param name="key">The name of the secret to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretProviderException">An error occurred while accessing OVHcloud.</exception>
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
                $"OVH error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in OVHcloud Secrets Management.
    /// </summary>
    /// <param name="key">The name of the secret.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of version information.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing OVHcloud.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _client.ListVersionsAsync(key, cancellationToken);

            var latest = versions.OrderByDescending(v => v.Id).FirstOrDefault();

            return versions
                .Select(v => new SecretVersionInfo
                {
                    Version = v.Id.ToString(),
                    CreatedAt = v.CreatedAt,
                    IsCurrent = v.Id == latest?.Id && v.State == "ACTIVE"
                })
                .ToList()
                .AsReadOnly();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"OVH error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in OVHcloud Secrets Management.
    /// </summary>
    /// <param name="key">The name of the secret to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing OVHcloud.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken);
            return secret is not null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new SecretProviderException(
                $"OVH error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying API client and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    private static int GetCurrentVersion(OVHSecretResult secret, string? requestedVersion)
    {
        if (int.TryParse(requestedVersion, out var parsed))
            return parsed;

        return secret.CurrentVersion > 0 ? secret.CurrentVersion : 1;
    }
}
