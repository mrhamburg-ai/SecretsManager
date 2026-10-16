using SecretsManager.Builder;

namespace SecretsManager.AzureKeyVault;

/// <summary>
/// Extension methods to register the Azure Key Vault provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Azure Key Vault provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but configuration
    /// comes from the external source.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configure">Optional callback to set the provider options.</param>
    /// <returns>The same builder for chaining.</returns>
    public static SecretProviderBuilder WithAzureKeyVault(
        this SecretProviderBuilder builder,
        Action<AzureKeyVaultOptions>? configure = null)
    {
        builder.RegisterProvider(new AzureKeyVaultSecretProviderFactory());

        if (configure is null) return builder;

        var options = new AzureKeyVaultOptions();
        configure(options);

        builder.UseProvider("azurekeyvault", settings =>
        {
            settings["vault.url"] = options.VaultUrl;

            if (options.AuthenticationType is not "default")
                settings["authentication.type"] = options.AuthenticationType;

            if (options.TenantId is not null)
                settings["authentication.tenantid"] = options.TenantId;

            if (options.ClientId is not null)
                settings["authentication.clientid"] = options.ClientId;

            if (options.ClientSecret is not null)
                settings["authentication.clientsecret"] = options.ClientSecret;
        });

        return builder;
    }
}
