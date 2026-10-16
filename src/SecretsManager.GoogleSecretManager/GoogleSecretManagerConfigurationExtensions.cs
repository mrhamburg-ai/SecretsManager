using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.GoogleSecretManager;

public static class GoogleSecretManagerConfigurationExtensions
{
    public static SecretProviderBuilder ConfigureGoogleSecretManager(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:googlesecretmanager").Get<GoogleSecretManagerOptions>()
            ?? new GoogleSecretManagerOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for GoogleSecretManager: {string.Join("; ", errors)}");

        return builder.WithGoogleSecretManager(o =>
        {
            o.ProjectId = options.ProjectId;
            o.CredentialsPath = options.CredentialsPath;
            o.CredentialsJson = options.CredentialsJson;
            o.Endpoint = options.Endpoint;
        });
    }
}
