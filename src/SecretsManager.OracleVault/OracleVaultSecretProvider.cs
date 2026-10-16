using Oci.Common;
using Oci.Common.Auth;
using Oci.Common.Model;
using SecretsManager.Internal;
using SecretsManager.OracleVault.Internal;

namespace SecretsManager.OracleVault;

/// <summary>
/// Stores and retrieves secrets from an Oracle Cloud Infrastructure (OCI) Vault.
/// Uses the OCI SDK secret client with configurable authentication
/// (config file, instance principal, or session token).
/// </summary>
public sealed class OracleVaultSecretProvider : ISecretProvider
{
    private readonly ISecretClientWrapper _client;
    private readonly OracleVaultOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleVaultSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Oracle Vault configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><see cref="OracleVaultOptions.AuthenticationType"/> is required.</exception>
    public OracleVaultSecretProvider(OracleVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        if (string.IsNullOrWhiteSpace(options.AuthenticationType))
            throw new ArgumentException("AuthenticationType is required.", nameof(options));

        var authProvider = CreateAuthProvider(options);
        _client = new SecretClientWrapper(
            authProvider,
            options.VaultId ?? string.Empty,
            options.CompartmentId ?? string.Empty,
            null,
            options.Region);
    }

    internal OracleVaultSecretProvider(ISecretClientWrapper client, OracleVaultOptions options)
    {
        _client = client;
        _options = options;
    }

    /// <summary>
    /// Retrieves a secret from the Oracle Vault by key.
    /// </summary>
    /// <param name="key">The secret name or OCID.</param>
    /// <param name="query">Optional query specifying the version and/or JSON property to extract.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value with version, content type, and tags.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">The Oracle Vault request failed.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSecretBundleAsync(key, query?.Version != null ? long.Parse(query.Version) : null, cancellationToken);
            var value = result.Value;

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.VersionNumber?.ToString(),
                CreatedAt = result.CreatedAt,
                UpdatedAt = result.CreatedAt,
                ContentType = result.ContentType,
                Tags = result.Tags
            };
        }
        catch (OciException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (OciException ex)
        {
            throw new SecretProviderException(
                $"Oracle Vault error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in the Oracle Vault.
    /// </summary>
    /// <param name="key">The secret name. When the key contains a slash, the last segment is used as the vault secret name.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata (content type and tags).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored secret value with its new version number.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">The Oracle Vault request failed, or required vault/compartment configuration is missing.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = metadata?.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var summary = await _client.GetSecretSummaryAsync(key, cancellationToken);

            OracleSecretResult result;

            if (summary != null)
            {
                var currentVersion = await _client.GetSecretBundleAsync(key, null, cancellationToken);
                result = await _client.UpdateSecretAsync(
                    key,
                    currentVersion.VersionNumber ?? 0,
                    value,
                    metadata?.ContentType,
                    tags,
                    cancellationToken);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_options.VaultId))
                    throw new SecretProviderException("VaultId is required to create a new secret. Configure it in OracleVaultOptions.");

                if (string.IsNullOrWhiteSpace(_options.CompartmentId))
                    throw new SecretProviderException("CompartmentId is required to create a new secret. Configure it in OracleVaultOptions.");

                var secretName = key.Contains('/') ? key.Split('/').Last() : key;
                result = await _client.CreateSecretAsync(
                    secretName,
                    _options.VaultId,
                    _options.CompartmentId,
                    value,
                    metadata?.ContentType,
                    tags,
                    cancellationToken);
            }

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = result.VersionNumber?.ToString(),
                CreatedAt = result.CreatedAt,
                UpdatedAt = result.CreatedAt,
                ContentType = metadata?.ContentType ?? result.ContentType,
                Tags = metadata?.Tags
            };
        }
        catch (OciException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (OciException ex)
        {
            throw new SecretProviderException(
                $"Oracle Vault error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from the Oracle Vault.
    /// </summary>
    /// <param name="key">The secret name or OCID to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">The Oracle Vault request failed.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (OciException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (OciException ex)
        {
            throw new SecretProviderException(
                $"Oracle Vault error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in the Oracle Vault.
    /// </summary>
    /// <param name="key">The secret name or OCID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The read-only list of secret version information.</returns>
    /// <exception cref="SecretNotFoundException">The secret was not found.</exception>
    /// <exception cref="SecretProviderException">The Oracle Vault request failed.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _client.GetSecretSummaryAsync(key, cancellationToken);
            if (summary == null)
                throw new SecretNotFoundException(key);

            var versions = new List<SecretVersionInfo>();
            await foreach (var version in _client.GetSecretVersionsAsync(key, cancellationToken))
            {
                versions.Add(new SecretVersionInfo
                {
                    Version = version.VersionNumber.ToString(),
                    CreatedAt = version.CreatedAt,
                    IsCurrent = version.IsCurrent
                });
            }

            return versions.AsReadOnly();
        }
        catch (OciException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            throw new SecretNotFoundException(key, ex);
        }
        catch (OciException ex)
        {
            throw new SecretProviderException(
                $"Oracle Vault error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in the Oracle Vault.
    /// </summary>
    /// <param name="key">The secret name or OCID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">The Oracle Vault request failed.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _client.GetSecretSummaryAsync(key, cancellationToken);
            return summary != null;
        }
        catch (OciException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            return false;
        }
        catch (OciException ex)
        {
            throw new SecretProviderException(
                $"Oracle Vault error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Releases the underlying OCI secret client and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    private static IBasicAuthenticationDetailsProvider CreateAuthProvider(OracleVaultOptions options)
    {
        return options.AuthenticationType.ToLowerInvariant() switch
        {
            "configfile" => new ConfigFileAuthenticationDetailsProvider(
                options.ProfileName ?? "DEFAULT",
                options.ConfigFilePath),
            "instanceprincipal" => new InstancePrincipalsAuthenticationDetailsProvider(),
            "securitytoken" => new SessionTokenAuthenticationDetailsProvider(
                options.ConfigFilePath),
            _ => throw new ArgumentException(
                $"Unknown authentication type '{options.AuthenticationType}'. " +
                "Supported: configfile, instanceprincipal, securitytoken.")
        };
    }
}
