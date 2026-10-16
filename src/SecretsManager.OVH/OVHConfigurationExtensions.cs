using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.OVH;

/// <summary>
/// Provides configuration binding for the OVHcloud Secrets Management provider from <see cref="IConfiguration"/>.
/// </summary>
public static class OVHConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:ovh</c> configuration section, validates the options, and registers the provider.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to extend.</param>
    /// <param name="configuration">The configuration containing the provider settings.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">One or more configuration values are invalid.</exception>
    public static SecretProviderBuilder ConfigureOVH(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:ovh").Get<OVHOptions>()
            ?? new OVHOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for OVH: {string.Join("; ", errors)}");

        return builder.WithOVH(o =>
        {
            o.Endpoint = options.Endpoint;
            o.ApplicationKey = options.ApplicationKey;
            o.ApplicationSecret = options.ApplicationSecret;
            o.ConsumerKey = options.ConsumerKey;
            o.OkmsId = options.OkmsId;
        });
    }
}
