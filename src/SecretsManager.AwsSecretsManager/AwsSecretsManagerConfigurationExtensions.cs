using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.AwsSecretsManager;

/// <summary>
/// Configuration bridge for [A-Zaaz] provider.
/// </summary>
public static class AwsSecretsManagerConfigurationExtensions
{
    public static SecretProviderBuilder ConfigureAwsSecretsManager(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:awssecretsmanager").Get<AwsSecretsManagerOptions>()
            ?? new AwsSecretsManagerOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for AwsSecretsManager: {string.Join("; ", errors)}");

        return builder.WithAwsSecretsManager(o =>
        {
            o.Region = options.Region;
            o.AccessKey = options.AccessKey;
            o.SecretKey = options.SecretKey;
            o.SessionToken = options.SessionToken;
        });
    }
}
