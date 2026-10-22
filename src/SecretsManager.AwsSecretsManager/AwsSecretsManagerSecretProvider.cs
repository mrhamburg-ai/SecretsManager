using Amazon.SecretsManager.Model;
using SecretsManager.AwsSecretsManager.Internal;
using SecretsManager.Internal;

namespace SecretsManager.AwsSecretsManager;

/// <summary>
/// Secret provider that stores and retrieves secrets using AWS Secrets Manager.
/// </summary>
public sealed class AwsSecretsManagerSecretProvider : ISecretProvider
{
    private readonly IAwsSecretsManagerClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwsSecretsManagerSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The AWS Secrets Manager configuration options.</param>
    /// <exception cref="ArgumentException">Thrown when required options are missing.</exception>
    public AwsSecretsManagerSecretProvider(AwsSecretsManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Region))
            throw new ArgumentException("Region is required.", nameof(options));

        _client = new AwsSecretsManagerClient(options);
    }

    internal AwsSecretsManagerSecretProvider(IAwsSecretsManagerClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Retrieves a secret value from AWS Secrets Manager by key.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="query">Optional query parameters for version or property selection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value with version, timestamp, and tag metadata.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the AWS API call fails.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretValueAsync(
                key, query?.Version, null, cancellationToken);

            var value = result.Value;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            var tags = result.Tags?.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.VersionId,
                CreatedAt = result.CreatedDate,
                Tags = tags?.AsReadOnly(),
            };
        }
        catch (ResourceNotFoundException ex)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"AWS Secrets Manager error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in AWS Secrets Manager.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional secret metadata including content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored secret value with the new version ID.</returns>
    /// <exception cref="SecretProviderException">Thrown when the AWS API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.PutSecretValueAsync(
                key, value, metadata?.ContentType, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.VersionId,
                CreatedAt = result.CreatedDate,
                ContentType = metadata?.ContentType,
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"AWS Secrets Manager error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from AWS Secrets Manager.
    /// </summary>
    /// <param name="key">The secret key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the AWS API call fails.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (ResourceNotFoundException ex)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"AWS Secrets Manager error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in AWS Secrets Manager.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of version information.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the AWS API call fails.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = new List<SecretVersionInfo>();
            await foreach (var v in _client.ListSecretVersionsAsync(key, cancellationToken))
            {
                versions.Add(new SecretVersionInfo
                {
                    Version = v.VersionId,
                    CreatedAt = v.CreatedDate,
                    IsCurrent = v.IsCurrent,
                });
            }

            return versions.AsReadOnly();
        }
        catch (SecretNotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"AWS Secrets Manager error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in AWS Secrets Manager.
    /// </summary>
    /// <param name="key">The secret key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the AWS API call fails.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.SecretExistsAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"AWS Secrets Manager error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying AWS Secrets Manager client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
