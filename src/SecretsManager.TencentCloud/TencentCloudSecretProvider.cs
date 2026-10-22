using System.Net.Http.Json;
using SecretsManager.Internal;
using SecretsManager.TencentCloud.Internal;

namespace SecretsManager.TencentCloud;

/// <summary>
/// Secret provider that stores and retrieves secrets using the Tencent Cloud Secrets Manager service.
/// </summary>
public sealed class TencentCloudSecretProvider : ISecretProvider
{
    private readonly ITencentCloudApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="TencentCloudSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Tencent Cloud configuration options.</param>
    public TencentCloudSecretProvider(TencentCloudOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Region))
            throw new ArgumentException("Region is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SecretId))
            throw new ArgumentException("SecretId is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            throw new ArgumentException("SecretKey is required.", nameof(options));

        _client = new TencentCloudApiClient(options);
    }

    internal TencentCloudSecretProvider(ITencentCloudApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Retrieves a secret value from Tencent Cloud Secrets Manager by key.
    /// </summary>
    /// <param name="key">The secret name to retrieve.</param>
    /// <param name="query">Optional query specifying a version ID or property to extract.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SecretValue"/> containing the secret data.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Tencent Cloud API call fails.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretValueAsync(
                key,
                versionId: query?.Version,
                cancellationToken);

            if (result is null)
                throw new SecretNotFoundException(key);

            var value = result.SecretValue;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.VersionId,
                CreatedAt = result.CreateTime,
                UpdatedAt = result.CreateTime
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Tencent Cloud error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in Tencent Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The secret name to create or update.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata (not currently used by this provider).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SecretValue"/> containing the stored secret data and the new version ID.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Tencent Cloud API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versionId = $"v-{Guid.NewGuid():N}";

            var existing = await _client.GetSecretValueAsync(
                key, versionId: null, cancellationToken);

            TencentCloudSecretResult result;

            if (existing is null)
            {
                result = await _client.CreateSecretAsync(key, value, versionId, cancellationToken);
            }
            else
            {
                result = await _client.PutSecretValueAsync(key, value, versionId, cancellationToken);
            }

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.VersionId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Tencent Cloud error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from Tencent Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The secret name to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretProviderException">Thrown when the Tencent Cloud API call fails.</exception>
    public async Task DeleteSecretAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Tencent Cloud error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret from Tencent Cloud Secrets Manager, ordered by creation time (newest first).
    /// </summary>
    /// <param name="key">The secret name to list versions for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SecretVersionInfo"/> describing each version.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Tencent Cloud API call fails.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _client.ListSecretVersionIdsAsync(key, cancellationToken);

            return versions
                .OrderByDescending(v => v.CreateTime)
                .Select(v => new SecretVersionInfo
                {
                    Version = v.VersionId,
                    CreatedAt = v.CreateTime,
                    IsCurrent = v.VersionStages?.Contains("Latest") == true
                })
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Tencent Cloud error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Tencent Cloud Secrets Manager.
    /// </summary>
    /// <param name="key">The secret name to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Tencent Cloud API call fails.</exception>
    public async Task<bool> SecretExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretValueAsync(
                key, versionId: null, cancellationToken);
            return result is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Tencent Cloud error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying Tencent Cloud API client and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
