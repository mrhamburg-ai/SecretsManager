using SecretsManager.Builder;

namespace SecretsManager.IBMCloudSecretsManager;

/// <summary>
/// Extension methods for registering the IBM Cloud Secrets Manager provider with a <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the IBM Cloud Secrets Manager provider factory and optionally configures it with fluent options.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to extend.</param>
    /// <param name="configure">An optional action to configure <see cref="IBMCloudSecretsManagerOptions"/>.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> instance for method chaining.</returns>
    public static SecretProviderBuilder WithIBMCloudSecretsManager(
        this SecretProviderBuilder builder,
        Action<IBMCloudSecretsManagerOptions>? configure = null)
    {
        builder.RegisterProvider(new IBMCloudSecretsManagerSecretProviderFactory());

        if (configure is null) return builder;

        var options = new IBMCloudSecretsManagerOptions();
        configure(options);

        builder.UseProvider("ibmcloud-secrets-manager", settings =>
        {
            settings["region"] = options.Region;
            settings["instance.id"] = options.InstanceId;
            settings["api.key"] = options.ApiKey;

            if (!string.IsNullOrEmpty(options.ServiceUrl))
                settings["service.url"] = options.ServiceUrl;
        });

        return builder;
    }
}
