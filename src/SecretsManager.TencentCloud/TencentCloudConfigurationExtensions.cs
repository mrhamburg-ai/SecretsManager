using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.TencentCloud;

/// <summary>
/// Extension methods for binding Tencent Cloud configuration from <see cref="IConfiguration"/>.
/// </summary>
public static class TencentCloudConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:tencentcloud</c> configuration section to <see cref="TencentCloudOptions"/>,
    /// validates the options, and registers the Tencent Cloud provider.
    /// </summary>
    /// <param name="builder">The <see cref="SecretProviderBuilder"/> to configure.</param>
    /// <param name="configuration">The configuration containing the <c>SecretsManager:tencentcloud</c> section.</param>
    /// <returns>The same <see cref="SecretProviderBuilder"/> for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when validation of the bound options fails.</exception>
    public static SecretProviderBuilder ConfigureTencentCloud(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:tencentcloud").Get<TencentCloudOptions>()
            ?? new TencentCloudOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for TencentCloud: {string.Join("; ", errors)}");

        return builder.WithTencentCloud(o =>
        {
            o.Region = options.Region;
            o.SecretId = options.SecretId;
            o.SecretKey = options.SecretKey;
            o.Endpoint = options.Endpoint;
        });
    }
}
