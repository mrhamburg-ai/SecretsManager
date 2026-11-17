using SecretsManager.Builder;

namespace SecretsManager.Conjur;

/// <summary>
/// Extension methods to register the CyberArk Conjur provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Conjur provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but configuration
    /// comes from the external source.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configure">Optional callback to set the provider options.</param>
    /// <returns>The same builder for chaining.</returns>
    public static SecretProviderBuilder WithConjur(
        this SecretProviderBuilder builder,
        Action<ConjurOptions>? configure = null)
    {
        builder.RegisterProvider(new ConjurSecretProviderFactory());

        if (configure is null) return builder;

        var options = new ConjurOptions();
        configure(options);

        builder.UseProvider("conjur", () => new ConjurSecretProvider(options));

        return builder;
    }
}