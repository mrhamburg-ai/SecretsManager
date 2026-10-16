using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Vault;

public static class VaultConfigurationExtensions
{
    public static SecretProviderBuilder ConfigureVault(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:vault").Get<VaultOptions>()
            ?? new VaultOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Vault: {string.Join("; ", errors)}");

        return builder.WithVault(o =>
        {
            o.Url = options.Url;
            o.Token = options.Token;
            o.MountPath = options.MountPath;
            o.SkipTlsVerify = options.SkipTlsVerify;
        });
    }
}
