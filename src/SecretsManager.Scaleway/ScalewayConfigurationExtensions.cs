using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Scaleway;

/// <summary>
/// Extension methods for binding the Scaleway provider configuration from <see cref="IConfiguration"/>.
/// </summary>
public static class ScalewayConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:scaleway</c> configuration section to <see cref="ScalewayOptions"/>,
    /// validates the options, and registers the Scaleway provider.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration to bind.</param>
    /// <returns>The same builder for method chaining.</returns>
    /// <exception cref="ArgumentException">The configuration is invalid.</exception>
    public static SecretProviderBuilder ConfigureScaleway(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:scaleway").Get<ScalewayOptions>()
            ?? new ScalewayOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Scaleway: {string.Join("; ", errors)}");

        return builder.WithScaleway(o =>
        {
            o.Region = options.Region;
            o.ProjectId = options.ProjectId;
            o.AccessKey = options.AccessKey;
            o.SecretKey = options.SecretKey;
        });
    }
}
