using SecretsManager.Builder;

namespace SecretsManager.Vault;

/// <summary>
/// Extension methods for registering the HashiCorp Vault secret provider with a <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the HashiCorp Vault secret provider factory and optionally configures it with fluent options.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to configure.</param>
    /// <param name="configure">An optional action to configure <see cref="VaultOptions"/>.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for method chaining.</returns>
    public static SecretProviderBuilder WithVault(
        this SecretProviderBuilder builder,
        Action<VaultOptions>? configure = null)
    {
        builder.RegisterProvider(new VaultSecretProviderFactory());

        if (configure is null) return builder;

        var options = new VaultOptions();
        configure(options);

        builder.UseProvider("vault", settings =>
        {
            if (!string.IsNullOrEmpty(options.Url))
                settings["url"] = options.Url;

            if (!string.IsNullOrEmpty(options.Token))
                settings["token"] = options.Token;

            if (!string.IsNullOrEmpty(options.MountPath))
                settings["mount.path"] = options.MountPath;

            if (options.SkipTlsVerify)
                settings["skip.tls.verify"] = "true";
        });

        return builder;
    }
}
