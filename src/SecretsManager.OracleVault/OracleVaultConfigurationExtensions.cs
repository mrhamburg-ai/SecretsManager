using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.OracleVault;

/// <summary>
/// Provides the <see cref="ConfigureOracleVault"/> configuration-section extension for the Oracle Vault provider.
/// </summary>
public static class OracleVaultConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:oraclevault</c> configuration section, validates the options,
    /// and registers the Oracle Vault provider.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentException">The bound options failed validation.</exception>
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
