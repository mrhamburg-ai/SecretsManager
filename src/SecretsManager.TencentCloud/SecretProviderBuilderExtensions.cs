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

        builder.UseProvider("tencentCloud", () => new TencentCloudSecretProvider(options));

        return builder;
    }
}
