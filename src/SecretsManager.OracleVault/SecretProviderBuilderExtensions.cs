using SecretsManager.Builder;

namespace SecretsManager.OracleVault;

/// <summary>
/// Extension methods to register the Oracle Vault provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Oracle Vault provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but
    /// configuration comes from the external source.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="OracleVaultOptions"/>.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for chaining.</returns>
    public static SecretProviderBuilder WithOracleVault(
        this SecretProviderBuilder builder,
        Action<OracleVaultOptions>? configure = null)
    {
        builder.RegisterProvider(new OracleVaultSecretProviderFactory());

        if (configure is null) return builder;

        var options = new OracleVaultOptions();
        configure(options);

        builder.UseProvider("oraclevault", settings =>
        {
            if (options.AuthenticationType is not "configfile")
                settings["authentication.type"] = options.AuthenticationType;

            if (options.ProfileName is not null)
                settings["authentication.profilename"] = options.ProfileName;

            if (options.ConfigFilePath is not null)
                settings["authentication.configfilepath"] = options.ConfigFilePath;

            if (options.Region is not null)
                settings["region"] = options.Region;

            if (options.VaultId is not null)
                settings["vault.id"] = options.VaultId;

            if (options.CompartmentId is not null)
                settings["compartment.id"] = options.CompartmentId;
        });

        return builder;
    }
}
