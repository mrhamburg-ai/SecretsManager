using SecretsManager.Builder;

namespace SecretsManager.GoogleSecretManager;

/// <summary>
/// Extension methods to register the Google Cloud Secret Manager provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Google Cloud Secret Manager provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but
    /// configuration comes from the external source.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="GoogleSecretManagerOptions"/>.</param>
    /// <returns>The secret provider builder.</returns>
    public static SecretProviderBuilder WithGoogleSecretManager(
        this SecretProviderBuilder builder,
        Action<GoogleSecretManagerOptions>? configure = null)
    {
        builder.RegisterProvider(new GoogleSecretManagerSecretProviderFactory());

        if (configure is null) return builder;

        var options = new GoogleSecretManagerOptions();
        configure(options);

        builder.UseProvider("googlesecretmanager", settings =>
        {
            settings["project.id"] = options.ProjectId;

            if (options.CredentialsPath is not null)
                settings["credentials.path"] = options.CredentialsPath;

            if (options.CredentialsJson is not null)
                settings["credentials.json"] = options.CredentialsJson;

            if (options.Endpoint is not null)
                settings["endpoint"] = options.Endpoint;
        });

        return builder;
    }
}
