using SecretsManager.Builder;

namespace SecretsManager.Scaleway;

/// <summary>
/// Extension methods for registering the Scaleway secret provider with a <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Scaleway provider factory and optionally applies fluent configuration.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="ScalewayOptions"/>.</param>
    /// <returns>The same builder for method chaining.</returns>
    public static SecretProviderBuilder WithScaleway(
        this SecretProviderBuilder builder,
        Action<ScalewayOptions>? configure = null)
    {
        builder.RegisterProvider(new ScalewaySecretProviderFactory());

        if (configure is null) return builder;

        var options = new ScalewayOptions();
        configure(options);

        builder.UseProvider("scaleway", () => new ScalewaySecretProvider(options));

        return builder;
    }
}
