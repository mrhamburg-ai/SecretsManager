using SecretsManager.Builder;

namespace SecretsManager.AliyunKms;

/// <summary>
/// Extension methods for registering the Alibaba Cloud KMS secret provider.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Alibaba Cloud KMS secret provider factory and optionally applies fluent configuration.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="AliyunKmsOptions"/>.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static SecretProviderBuilder WithAliyunKms(
        this SecretProviderBuilder builder,
        Action<AliyunKmsOptions>? configure = null)
    {
        builder.RegisterProvider(new AliyunKmsSecretProviderFactory());

        if (configure is null) return builder;

        var options = new AliyunKmsOptions();
        configure(options);

        builder.UseProvider("aliyunKms", settings =>
        {
            if (!string.IsNullOrEmpty(options.Region))
                settings["region"] = options.Region;

            if (!string.IsNullOrEmpty(options.AccessKeyId))
                settings["access.key"] = options.AccessKeyId;

            if (!string.IsNullOrEmpty(options.AccessKeySecret))
                settings["secret.key"] = options.AccessKeySecret;

            if (!string.IsNullOrEmpty(options.Endpoint))
                settings["endpoint"] = options.Endpoint;
        });

        return builder;
    }
}
