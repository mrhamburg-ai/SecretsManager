using SecretsManager.Builder;

namespace SecretsManager.Passbolt;

/// <summary>
/// Extension methods to register the Passbolt provider with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Passbolt provider and optionally configures it via the fluent API.
    /// When combined with <see cref="SecretProviderBuilder.FromEnvironment"/> or
    /// <see cref="SecretProviderBuilder.FromYaml"/>, the factory is registered but
    /// configuration comes from the external source.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="PassboltOptions"/>.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for chaining.</returns>
    public static SecretProviderBuilder WithPassbolt(
        this SecretProviderBuilder builder,
        Action<PassboltOptions>? configure = null)
    {
        builder.RegisterProvider(new PassboltSecretProviderFactory());

        if (configure is null) return builder;

        var options = new PassboltOptions();
        configure(options);

        builder.UseProvider("passbolt", settings =>
        {
            if (!string.IsNullOrEmpty(options.BaseUrl))
                settings["base.url"] = options.BaseUrl;

            if (!string.IsNullOrEmpty(options.UserPrivateKey))
                settings["user.private.key"] = options.UserPrivateKey;

            if (!string.IsNullOrEmpty(options.UserPrivateKeyPassphrase))
                settings["user.private.passphrase"] = options.UserPrivateKeyPassphrase;

            if (!string.IsNullOrEmpty(options.UserKeyFingerprint))
                settings["user.key.fingerprint"] = options.UserKeyFingerprint;
        });

        return builder;
    }
}
