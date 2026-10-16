using SecretsManager.Internal;
using SecretsManager.Scaleway.Internal;

namespace SecretsManager.Scaleway;

/// <summary>
/// An <see cref="ISecretProvider"/> backed by the Scaleway Secret Manager API.
/// </summary>
public sealed class ScalewaySecretProvider : ISecretProvider
{
    private readonly IScalewayApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScalewaySecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Scaleway API connection options.</param>
    public ScalewaySecretProvider(ScalewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Region))
            throw new ArgumentException("Region is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ProjectId))
            throw new ArgumentException("ProjectId is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            throw new ArgumentException("SecretKey is required.", nameof(options));

        _client = new ScalewayApiClient(options);
    }

    internal ScalewaySecretProvider(IScalewayApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret by name from Scaleway Secret Manager.
    /// When <paramref name="query"/> specifies a version, that revision is returned;
    /// otherwise the latest enabled revision is returned. When <paramref name="query"/>
    /// specifies a property, only that property is extracted from the (JSON) value.
    /// </summary>
    /// <param name="key">The secret name to look up.</param>
    /// <param name="query">Optional version or property filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The secret value with metadata.</returns>
    /// <exception cref="SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="SecretProviderException">A Scaleway API error occurred.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.FindSecretByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var revision = query?.Version ?? "latest_enabled";
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
                $"Scaleway error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in Scaleway Secret Manager.
    /// If the secret does not exist it is created; otherwise a new version is added.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional tags to attach on creation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored secret value with the new version information.</returns>
    /// <exception cref="SecretProviderException">A Scaleway API error occurred.</exception>
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
                $"Scaleway error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret and all its versions from Scaleway Secret Manager.
    /// </summary>
    /// <param name="key">The secret name to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="SecretProviderException">A Scaleway API error occurred.</exception>
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
                $"Scaleway error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret stored in Scaleway Secret Manager.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of version information.</returns>
    /// <exception cref="SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="SecretProviderException">A Scaleway API error occurred.</exception>
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
                $"Scaleway error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Scaleway Secret Manager without retrieving its value.
    /// </summary>
    /// <param name="key">The secret name to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">A Scaleway API error occurred.</exception>
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
                $"Scaleway error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying Scaleway API client resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
