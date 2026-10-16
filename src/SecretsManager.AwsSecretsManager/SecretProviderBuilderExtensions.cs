using SecretsManager.Builder;

namespace SecretsManager.AwsSecretsManager;

/// <summary>
/// Extension methods for registering the AWS Secrets Manager secret provider.
/// </summary>
public static class SecretProviderBuilderExtensions
{
    /// <summary>
    /// Registers the AWS Secrets Manager secret provider factory and optionally applies fluent configuration.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="AwsSecretsManagerOptions"/>.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static SecretProviderBuilder WithAwsSecretsManager(
        this SecretProviderBuilder builder,
        Action<AwsSecretsManagerOptions>? configure = null)
    {
        builder.RegisterProvider(new AwsSecretsManagerSecretProviderFactory());

        if (configure is null) return builder;

        var options = new AwsSecretsManagerOptions();
        configure(options);

        builder.UseProvider("awssecretsmanager", settings =>
        {
            settings["region"] = options.Region;

            if (!string.IsNullOrEmpty(options.AccessKey))
                settings["access.key"] = options.AccessKey;

            if (!string.IsNullOrEmpty(options.SecretKey))
                settings["secret.key"] = options.SecretKey;

            if (!string.IsNullOrEmpty(options.SessionToken))
                settings["session.token"] = options.SessionToken;
        });

        return builder;
    }
}
