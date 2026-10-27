using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Infisical;

/// <summary>
/// Binds <see cref="InfisicalOptions"/> from configuration and registers the Infisical provider
/// with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class InfisicalConfigurationExtensions
{
    /// <summary>
    /// Reads the <c>SecretsManager:Infisical</c> configuration section, validates it and registers
    /// the Infisical provider.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the bound options are invalid.</exception>
    public static SecretProviderBuilder ConfigureInfisical(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:Infisical").Get<InfisicalOptions>()
            ?? new InfisicalOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Infisical: {string.Join("; ", errors)}");

        return builder.WithInfisical(o =>
        {
            o.HostUri = options.HostUri;
            o.ClientId = options.ClientId;
            o.ClientSecret = options.ClientSecret;
            o.ProjectId = options.ProjectId;
            o.EnvironmentSlug = options.EnvironmentSlug;
            o.SecretPath = options.SecretPath;
            o.ExpandSecretReferences = options.ExpandSecretReferences;
        });
    }
}
