using SecretsManager.Builder;

namespace SecretsManager.AzureKeyVault;

/// <summary>
/// A factory that creates <see cref="AzureKeyVaultSecretProvider"/> instances from a flat configuration dictionary.
/// </summary>
public sealed class AzureKeyVaultSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML or environment configuration: <c>"azurekeyvault"</c>.
    /// </summary>
    public string ProviderName => "azurekeyvault";

    /// <summary>
    /// Creates an <see cref="AzureKeyVaultSecretProvider"/> from the provided configuration dictionary.
    /// </summary>
    /// <param name="configuration">A dictionary of configuration key-value pairs. Recognized keys: <c>vaulturl</c>, <c>authentication.type</c>, <c>authentication.tenantid</c>, <c>authentication.clientid</c>, <c>authentication.clientsecret</c>.</param>
    /// <returns>A configured <see cref="AzureKeyVaultSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new AzureKeyVaultSecretProvider(options);
    }

    internal static AzureKeyVaultOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new AzureKeyVaultOptions();

        if (configuration.TryGetValue("vaulturl", out var vaultUrl) && !string.IsNullOrWhiteSpace(vaultUrl))
            options.VaultUrl = vaultUrl;

        if (configuration.TryGetValue("authentication.type", out var authType) && !string.IsNullOrWhiteSpace(authType))
            options.AuthenticationType = authType;

        if (configuration.TryGetValue("authentication.tenantid", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId))
            options.TenantId = tenantId;

        if (configuration.TryGetValue("authentication.clientid", out var clientId) && !string.IsNullOrWhiteSpace(clientId))
            options.ClientId = clientId;

        if (configuration.TryGetValue("authentication.clientsecret", out var clientSecret) && !string.IsNullOrWhiteSpace(clientSecret))
            options.ClientSecret = clientSecret;

        return options;
    }
}
