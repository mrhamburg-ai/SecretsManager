using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Passbolt;

public static class PassboltConfigurationExtensions
{
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
