using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.IBMCloudSecretsManager;

/// <summary>
/// Provides configuration binding for the IBM Cloud Secrets Manager provider from <see cref="IConfiguration"/>.
/// </summary>
public static class IBMCloudSecretsManagerConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:ibmcloudsecretsmanager</c> configuration section, validates the options, and registers the provider.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to extend.</param>
    /// <param name="configuration">The configuration containing the provider settings.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">One or more configuration values are invalid.</exception>
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
