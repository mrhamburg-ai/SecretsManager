using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.AliyunKms;

/// <summary>
/// Configuration bridge for the Alibaba Cloud KMS secret provider.
/// </summary>
public static class AliyunKmsConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:aliyunkms</c> configuration section to <see cref="AliyunKmsOptions"/>,
    /// validates the options, and registers the provider.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static SecretProviderBuilder ConfigureAliyunKms(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:aliyunkms").Get<AliyunKmsOptions>()
            ?? new AliyunKmsOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for AliyunKms: {string.Join("; ", errors)}");

        return builder.WithAliyunKms(o =>
        {
            o.Region = options.Region;
            o.AccessKeyId = options.AccessKeyId;
            o.AccessKeySecret = options.AccessKeySecret;
            o.Endpoint = options.Endpoint;
        });
    }
}
