using SecretsManager.Builder;

namespace SecretsManager.OVH;

/// <summary>
/// Extension methods for registering the OVHcloud Secrets Management provider with a <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the OVHcloud Secrets Management provider factory and optionally configures it with fluent options.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to extend.</param>
    /// <param name="configure">An optional action to configure <see cref="OVHOptions"/>.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> instance for method chaining.</returns>
    public static SecretProviderBuilder WithOVH(
        this SecretProviderBuilder builder,
        Action<OVHOptions>? configure = null)
    {
        builder.RegisterProvider(new OVHSecretProviderFactory());

        if (configure is null) return builder;

        var options = new OVHOptions();
        configure(options);

        builder.UseProvider("ovh", () => new OVHSecretProvider(options));

        return builder;
    }
}
