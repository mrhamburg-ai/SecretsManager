using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.OracleVault;

public static class OracleVaultConfigurationExtensions
{
    public static SecretProviderBuilder ConfigureOracleVault(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:oraclevault").Get<OracleVaultOptions>()
            ?? new OracleVaultOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for OracleVault: {string.Join("; ", errors)}");

        return builder.WithOracleVault(o =>
        {
            o.AuthenticationType = options.AuthenticationType;
            o.ProfileName = options.ProfileName;
            o.ConfigFilePath = options.ConfigFilePath;
            o.Region = options.Region;
            o.VaultId = options.VaultId;
            o.CompartmentId = options.CompartmentId;
        });
    }
}
