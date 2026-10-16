using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Conjur;

/// <summary>
/// Configuration bridge for [A-Zaaz] provider.
/// </summary>
public static class ConjurConfigurationExtensions
{
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
