using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Passbolt;

/// <summary>
/// Provides the <see cref="ConfigurePassbolt"/> configuration-section extension for the Passbolt provider.
/// </summary>
public static class PassboltConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:passbolt</c> configuration section, validates the options,
    /// and registers the Passbolt provider.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentException">The bound options failed validation.</exception>
    public static SecretProviderBuilder ConfigurePassbolt(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:passbolt").Get<PassboltOptions>()
            ?? new PassboltOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Passbolt: {string.Join("; ", errors)}");

        return builder.WithPassbolt(o =>
        {
            o.BaseUrl = options.BaseUrl;
            o.UserPrivateKey = options.UserPrivateKey;
            o.UserPrivateKeyPassphrase = options.UserPrivateKeyPassphrase;
            o.UserKeyFingerprint = options.UserKeyFingerprint;
        });
    }
}
