using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using SecretsManager.AzureKeyVault.Internal;
using SecretsManager.Internal;

namespace SecretsManager.AzureKeyVault;

/// <summary>
/// An <see cref="ISecretProvider"/> implementation that stores and retrieves secrets in Azure Key Vault
/// using the <see cref="Azure.Security.KeyVault.Secrets.SecretClient"/>.
/// </summary>
public sealed class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly ISecretClientWrapper _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyVaultSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Azure Key Vault configuration options.</param>
    public AzureKeyVaultSecretProvider(AzureKeyVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.VaultUrl))
            throw new ArgumentException("VaultUrl is required.", nameof(options));

        var credential = CreateCredential(options);
        var client = new SecretClient(new Uri(options.VaultUrl), credential);
        _client = new SecretClientWrapper(client);
    }

    internal AzureKeyVaultSecretProvider(ISecretClientWrapper client)
    {
        _client = client;
    }

    /// <summary>
    /// Retrieves a secret from Azure Key Vault by key.
    /// </summary>
    /// <param name="key">The name of the secret to retrieve.</param>
    /// <param name="query">Optional query parameters to select a specific version or extract a JSON property.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SecretValue"/> containing the secret data and metadata.</returns>
    /// <exception cref="SecretNotFoundException">The secret does not exist in the vault.</exception>
    /// <exception cref="SecretProviderException">An error occurred while communicating with Azure Key Vault.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretAsync(key, query?.Version, cancellationToken);
            var value = result.Value;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.Version,
                CreatedAt = result.CreatedOn,
                UpdatedAt = result.UpdatedOn,
                ContentType = result.ContentType,
                Tags = result.Tags?.AsReadOnly()
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (RequestFailedException ex)
        {
            throw new SecretProviderException(
                $"Azure Key Vault error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in Azure Key Vault.
    /// </summary>
    /// <param name="key">The name of the secret to create or update.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata including content type and tags.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SecretValue"/> containing the stored secret data and metadata.</returns>
    /// <exception cref="SecretProviderException">An error occurred while communicating with Azure Key Vault.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = metadata?.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var result = await _client.SetSecretAsync(
                key, value, metadata?.ContentType, tags, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.Version,
                CreatedAt = result.CreatedOn,
                UpdatedAt = result.UpdatedOn,
                ContentType = metadata?.ContentType ?? result.ContentType,
                Tags = metadata?.Tags
            };
        }
        catch (RequestFailedException ex)
        {
            throw new SecretProviderException(
                $"Azure Key Vault error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from Azure Key Vault.
    /// </summary>
    /// <param name="key">The name of the secret to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="SecretNotFoundException">The secret does not exist in the vault.</exception>
    /// <exception cref="SecretProviderException">An error occurred while communicating with Azure Key Vault.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (RequestFailedException ex)
        {
            throw new SecretProviderException(
                $"Azure Key Vault error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in Azure Key Vault.
    /// </summary>
    /// <param name="key">The name of the secret.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SecretVersionInfo"/> objects representing each version.</returns>
    /// <exception cref="SecretNotFoundException">The secret does not exist in the vault.</exception>
    /// <exception cref="SecretProviderException">An error occurred while communicating with Azure Key Vault.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current version to determine IsCurrent flag
            string? currentVersion;
            try
            {
                var current = await _client.GetSecretAsync(key, null, cancellationToken);
                currentVersion = current.Version;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new SecretNotFoundException(key, ex);
            }

            var versions = new List<SecretVersionInfo>();
            await foreach (var sp in _client.GetSecretVersionsAsync(key, cancellationToken))
            {
                versions.Add(new SecretVersionInfo
                {
                    Version = sp.Version,
                    CreatedAt = sp.CreatedOn,
                    IsCurrent = sp.Version == currentVersion
                });
            }

            return versions.AsReadOnly();
        }
        catch (RequestFailedException ex)
        {
            throw new SecretProviderException(
                $"Azure Key Vault error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Azure Key Vault.
    /// </summary>
    /// <param name="key">The name of the secret to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">An error occurred while communicating with Azure Key Vault.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetSecretAsync(key, null, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
        catch (RequestFailedException ex)
        {
            throw new SecretProviderException(
                $"Azure Key Vault error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying <see cref="Azure.Security.KeyVault.Secrets.SecretClient"/> resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    private static Azure.Core.TokenCredential CreateCredential(AzureKeyVaultOptions options)
    {
        return options.AuthenticationType.ToLowerInvariant() switch
        {
            "default" or "" => new DefaultAzureCredential(),
            "serviceprincipal" => new ClientSecretCredential(
                options.TenantId ?? throw new ArgumentException("TenantId is required for service principal authentication."),
                options.ClientId ?? throw new ArgumentException("ClientId is required for service principal authentication."),
                options.ClientSecret ?? throw new ArgumentException("ClientSecret is required for service principal authentication.")),
            "managedidentity" => options.ClientId is not null
                ? new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(options.ClientId))
                : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            "workloadidentity" => new WorkloadIdentityCredential(
                new WorkloadIdentityCredentialOptions
                {
                    TenantId = options.TenantId ?? "",
                    ClientId = options.ClientId ?? ""
                }),
            _ => throw new ArgumentException(
                $"Unknown authentication type '{options.AuthenticationType}'. " +
                "Supported: default, serviceprincipal, managedidentity, workloadidentity.")
        };
    }
}
