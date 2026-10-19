using Google.Cloud.SecretManager.V1;
using Grpc.Core;
using SecretsManager.GoogleSecretManager.Internal;
using SecretsManager.Internal;

namespace SecretsManager.GoogleSecretManager;

/// <summary>
/// Stores and retrieves secrets from Google Cloud Secret Manager.
/// Supports versioning, metadata, labels, and JSON property extraction.
/// </summary>
public sealed class GoogleSecretManagerSecretProvider : ISecretProvider
{
    private readonly ISecretManagerClientWrapper _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleSecretManagerSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Google Secret Manager options.</param>
    public GoogleSecretManagerSecretProvider(GoogleSecretManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProjectId))
            throw new ArgumentException("ProjectId is required.", nameof(options));

        var client = CreateClient(options);
        _client = new SecretManagerClientWrapper(client, options.ProjectId);
    }

    internal GoogleSecretManagerSecretProvider(ISecretManagerClientWrapper client)
    {
        _client = client;
    }

    /// <summary>
    /// Reads a secret from Google Cloud Secret Manager, optionally extracting a JSON property.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="query">The secret query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing Google Cloud Secret Manager.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.AccessSecretVersionAsync(key, query?.Version, cancellationToken);
            var value = result.Value;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.Version,
                CreatedAt = result.CreatedAt,
                UpdatedAt = result.UpdatedAt,
                ContentType = result.ContentType,
                Tags = result.Labels?.AsReadOnly()
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (RpcException ex)
        {
            throw new SecretProviderException(
                $"Google Secret Manager error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes a secret to Google Cloud Secret Manager, creating the secret if it does not exist.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="value">The secret value.</param>
    /// <param name="metadata">The secret metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing Google Cloud Secret Manager.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var labels = metadata?.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            try
            {
                var result = await _client.AddSecretVersionAsync(
                    key, value, metadata?.ContentType, labels, cancellationToken);

                return new SecretValue
                {
                    Key = key,
                    Value = value,
                    Version = result.Version,
                    CreatedAt = result.CreatedAt,
                    ContentType = metadata?.ContentType,
                    Tags = metadata?.Tags
                };
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                await _client.CreateSecretAsync(key, metadata?.ContentType, labels, cancellationToken);
                var result = await _client.AddSecretVersionAsync(
                    key, value, metadata?.ContentType, labels, cancellationToken);

                return new SecretValue
                {
                    Key = key,
                    Value = value,
                    Version = result.Version,
                    CreatedAt = result.CreatedAt,
                    ContentType = metadata?.ContentType,
                    Tags = metadata?.Tags
                };
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (RpcException ex)
        {
            throw new SecretProviderException(
                $"Google Secret Manager error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from Google Cloud Secret Manager.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing Google Cloud Secret Manager.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (RpcException ex)
        {
            throw new SecretProviderException(
                $"Google Secret Manager error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all enabled versions of a secret in Google Cloud Secret Manager.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of secret versions.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">An error occurred while accessing Google Cloud Secret Manager.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            string? currentVersion;
            try
            {
                var current = await _client.AccessSecretVersionAsync(key, "latest", cancellationToken);
                currentVersion = current.Version;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new SecretNotFoundException(key, ex);
            }

            var versions = new List<SecretVersionInfo>();
            await foreach (var v in _client.ListSecretVersionsAsync(key, cancellationToken))
            {
                if (v.State == "ENABLED")
                {
                    versions.Add(new SecretVersionInfo
                    {
                        Version = v.Version,
                        CreatedAt = v.CreatedAt,
                        IsCurrent = v.Version == currentVersion
                    });
                }
            }

            return versions.AsReadOnly();
        }
        catch (RpcException ex)
        {
            throw new SecretProviderException(
                $"Google Secret Manager error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Google Cloud Secret Manager.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">An error occurred while accessing Google Cloud Secret Manager.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.SecretExistsAsync(key, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw new SecretProviderException(
                $"Google Secret Manager error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Releases the underlying Google Cloud Secret Manager client resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    private static SecretManagerServiceClient CreateClient(GoogleSecretManagerOptions options)
    {
        var builder = new SecretManagerServiceClientBuilder();

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
            builder.Endpoint = options.Endpoint;

#pragma warning disable CS0618
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
            builder.CredentialsPath = options.CredentialsPath;
        else if (!string.IsNullOrWhiteSpace(options.CredentialsJson))
            builder.JsonCredentials = options.CredentialsJson;
#pragma warning restore CS0618

        return builder.Build();
    }
}
