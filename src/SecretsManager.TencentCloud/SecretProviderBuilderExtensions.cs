using SecretsManager.Builder;

namespace SecretsManager.TencentCloud;

/// <summary>
/// Extension methods for registering the Tencent Cloud secret provider with a <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Tencent Cloud secret provider factory and optionally configures it with fluent options.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to configure.</param>
    /// <param name="configure">An optional action to configure <see cref="TencentCloudOptions"/>.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for method chaining.</returns>
    public static SecretProviderBuilder WithTencentCloud(
        this SecretProviderBuilder builder,
        Action<TencentCloudOptions>? configure = null)
    {
        builder.RegisterProvider(new TencentCloudSecretProviderFactory());

        if (configure is null) return builder;

        var options = new TencentCloudOptions();
        configure(options);

        builder.UseProvider("tencentCloud", settings =>
        {
            if (!string.IsNullOrEmpty(options.Region))
                settings["region"] = options.Region;

            if (!string.IsNullOrEmpty(options.SecretId))
                settings["secret.id"] = options.SecretId;

            if (!string.IsNullOrEmpty(options.SecretKey))
                settings["secret.key"] = options.SecretKey;

            if (!string.IsNullOrEmpty(options.Endpoint))
                settings["endpoint"] = options.Endpoint;
        });

        return builder;
    }
}
