using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.AzureKeyVault;

/// <summary>
/// Binds <see cref="AzureKeyVaultOptions"/> from configuration and registers the Azure Key Vault
/// provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class AzureKeyVaultConfigurationExtensions
{
    /// <summary>
    /// Reads the <c>SecretsManager:azurekeyvault</c> configuration section, validates it and
    /// registers the Azure Key Vault provider.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the bound options are invalid.</exception>
    public static SecretProviderBuilder ConfigureAzureKeyVault(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:azurekeyvault").Get<AzureKeyVaultOptions>()
            ?? new AzureKeyVaultOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for AzureKeyVault: {string.Join("; ", errors)}");

        return builder.WithAzureKeyVault(o =>
        {
            o.VaultUrl = options.VaultUrl;
            o.AuthenticationType = options.AuthenticationType;
            o.TenantId = options.TenantId;
            o.ClientId = options.ClientId;
            o.ClientSecret = options.ClientSecret;
        });
    }
}
