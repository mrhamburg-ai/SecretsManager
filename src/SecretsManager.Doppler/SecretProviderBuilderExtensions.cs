using SecretsManager.Builder;

namespace SecretsManager.Doppler;

/// <summary>
/// Extension methods to register the Doppler provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Doppler provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but configuration
    /// comes from the external source.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configure">Optional callback to set the provider options.</param>
    /// <returns>The same builder for chaining.</returns>
    public static SecretProviderBuilder WithDoppler(
        this SecretProviderBuilder builder,
        Action<DopplerOptions>? configure = null)
    {
        builder.RegisterProvider(new DopplerSecretProviderFactory());

        if (configure is null) return builder;

        var options = new DopplerOptions();
        configure(options);

        builder.UseProvider("doppler", settings =>
        {
            if (!string.IsNullOrEmpty(options.ApiUrl))
                settings["api.url"] = options.ApiUrl;

            if (!string.IsNullOrEmpty(options.Token))
                settings["token"] = options.Token;

            if (!string.IsNullOrEmpty(options.Project))
                settings["project"] = options.Project;

            if (!string.IsNullOrEmpty(options.Config))
                settings["config"] = options.Config;

            settings["use.raw.values"] = options.UseRawValues ? "true" : "false";
        });

        return builder;
    }
}
