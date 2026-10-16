using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.IBMCloudSecretsManager;

public static class IBMCloudSecretsManagerConfigurationExtensions
{
    public static SecretProviderBuilder ConfigureIBMCloudSecretsManager(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:ibmcloudsecretsmanager").Get<IBMCloudSecretsManagerOptions>()
            ?? new IBMCloudSecretsManagerOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for IBMCloudSecretsManager: {string.Join("; ", errors)}");

        return builder.WithIBMCloudSecretsManager(o =>
        {
            o.Region = options.Region;
            o.InstanceId = options.InstanceId;
            o.ApiKey = options.ApiKey;
            o.ServiceUrl = options.ServiceUrl;
        });
    }
}
