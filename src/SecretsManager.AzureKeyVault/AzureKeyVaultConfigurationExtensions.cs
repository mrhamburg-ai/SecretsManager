using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.AzureKeyVault;

public static class AzureKeyVaultConfigurationExtensions
{
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
