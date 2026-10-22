using System.Net.Http.Json;
using SecretsManager.Internal;
using SecretsManager.AliyunKms.Internal;

namespace SecretsManager.AliyunKms;

/// <summary>
/// Secret provider that stores and retrieves secrets using Alibaba Cloud KMS.
/// </summary>
public sealed class AliyunKmsSecretProvider : ISecretProvider
{
    private readonly IAliyunKmsApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AliyunKmsSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Alibaba Cloud KMS configuration options.</param>
    /// <exception cref="ArgumentException">Thrown when required options are missing.</exception>
    public AliyunKmsSecretProvider(AliyunKmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Region))
            throw new ArgumentException("Region is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
            throw new ArgumentException("AccessKeyId is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.AccessKeySecret))
            throw new ArgumentException("AccessKeySecret is required.", nameof(options));

        _client = new AliyunKmsApiClient(options);
    }

    internal AliyunKmsSecretProvider(IAliyunKmsApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Retrieves a secret value from Alibaba Cloud KMS by key.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="query">Optional query parameters for version or property selection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value with version and timestamp metadata.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the KMS API call fails.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretValueAsync(
                key,
                versionStage: null,
                versionId: query?.Version,
                cancellationToken);

            if (result is null)
                throw new SecretNotFoundException(key);

            var value = result.SecretData;

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
                $"Aliyun KMS error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in Alibaba Cloud KMS.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional secret metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored secret value with the new version ID.</returns>
    /// <exception cref="SecretProviderException">Thrown when the KMS API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versionId = $"v-{Guid.NewGuid():N}";

            var existing = await _client.GetSecretValueAsync(
                key, versionStage: null, versionId: null, cancellationToken);

            AliyunSecretResult result;

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
                $"Aliyun KMS error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from Alibaba Cloud KMS.
    /// </summary>
    /// <param name="key">The secret key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="SecretProviderException">Thrown when the KMS API call fails.</exception>
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
                $"Aliyun KMS error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in Alibaba Cloud KMS, ordered by creation time (newest first).
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of version information.</returns>
    /// <exception cref="SecretProviderException">Thrown when the KMS API call fails.</exception>
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
                    IsCurrent = v.VersionStages?.Contains("ACSCurrent") == true
                })
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Aliyun KMS error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Alibaba Cloud KMS.
    /// </summary>
    /// <param name="key">The secret key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the KMS API call fails.</exception>
    public async Task<bool> SecretExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretValueAsync(
                key, versionStage: null, versionId: null, cancellationToken);
            return result is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Aliyun KMS error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying Alibaba Cloud KMS API client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
