using SecretsManager.Builder;

namespace SecretsManager.Infisical;

/// <summary>
/// Extension methods to register the Infisical provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Infisical provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but configuration
    /// comes from the external source.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configure">Optional callback to set the provider options.</param>
    /// <returns>The same builder for chaining.</returns>
    public static SecretProviderBuilder WithInfisical(
        this SecretProviderBuilder builder,
        Action<InfisicalOptions>? configure = null)
    {
        builder.RegisterProvider(new InfisicalSecretProviderFactory());

        if (configure is null) return builder;

        var options = new InfisicalOptions();
        configure(options);

        builder.UseProvider("infisical", () => new InfisicalSecretProvider(options));

        return builder;
    }
}
