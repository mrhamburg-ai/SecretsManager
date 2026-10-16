using SecretsManager.Builder;

namespace SecretsManager.OracleVault;

/// <summary>
/// Factory that creates an <see cref="OracleVaultSecretProvider"/> from a flat configuration dictionary.
/// Used by <see cref="SecretProviderBuilder"/> when the "oraclevault" provider is selected.
/// </summary>
public sealed class OracleVaultSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML or environment configuration.
    /// </summary>
    public string ProviderName => "oraclevault";

    /// <summary>
    /// Creates an <see cref="OracleVaultSecretProvider"/> from the supplied configuration.
    /// </summary>
    /// <param name="configuration">
    /// The flat settings dictionary. Recognized keys:
    /// <c>authentication.type</c>, <c>authentication.profilename</c>, <c>authentication.configfilepath</c>,
    /// <c>region</c>, <c>vault.id</c>, and <c>compartment.id</c>.
    /// </param>
    /// <returns>The configured <see cref="OracleVaultSecretProvider"/>.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new OracleVaultSecretProvider(options);
    }

    internal static OracleVaultOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new OracleVaultOptions();

        if (configuration.TryGetValue("authentication.type", out var authType) && !string.IsNullOrWhiteSpace(authType))
            options.AuthenticationType = authType;

        if (configuration.TryGetValue("authentication.profilename", out var profileName) && !string.IsNullOrWhiteSpace(profileName))
            options.ProfileName = profileName;

        if (configuration.TryGetValue("authentication.configfilepath", out var configFilePath) && !string.IsNullOrWhiteSpace(configFilePath))
            options.ConfigFilePath = configFilePath;

        if (configuration.TryGetValue("region", out var region) && !string.IsNullOrWhiteSpace(region))
            options.Region = region;

        if (configuration.TryGetValue("vault.id", out var vaultId) && !string.IsNullOrWhiteSpace(vaultId))
            options.VaultId = vaultId;

        if (configuration.TryGetValue("compartment.id", out var compartmentId) && !string.IsNullOrWhiteSpace(compartmentId))
            options.CompartmentId = compartmentId;

        return options;
    }
}
