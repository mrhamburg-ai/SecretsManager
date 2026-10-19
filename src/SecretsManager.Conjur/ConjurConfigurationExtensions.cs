using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Conjur;

/// <summary>
/// Binds <see cref="ConjurOptions"/> from configuration and registers the Conjur provider
/// with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class ConjurConfigurationExtensions
{
    /// <summary>
    /// Reads the <c>SecretsManager:conjur</c> configuration section, validates it and registers the
    /// Conjur provider.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the bound options are invalid.</exception>
    public static SecretProviderBuilder ConfigureConjur(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:conjur").Get<ConjurOptions>()
            ?? new ConjurOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Conjur: {string.Join("; ", errors)}");

        return builder.WithConjur(o =>
        {
            o.Url = options.Url;
            o.Account = options.Account;
            o.Login = options.Login;
            o.ApiKey = options.ApiKey;
            o.PolicyPath = options.PolicyPath;
            o.SkipTlsVerify = options.SkipTlsVerify;
        });
    }
}
