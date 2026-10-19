using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Vault;

/// <summary>
/// Extension methods for binding HashiCorp Vault configuration from <see cref="IConfiguration"/>.
/// </summary>
public static class VaultConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:vault</c> configuration section to <see cref="VaultOptions"/>,
    /// validates the options, and registers the Vault provider.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to configure.</param>
    /// <param name="configuration">The configuration containing the <c>SecretsManager:vault</c> section.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when validation of the bound options fails.</exception>
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
