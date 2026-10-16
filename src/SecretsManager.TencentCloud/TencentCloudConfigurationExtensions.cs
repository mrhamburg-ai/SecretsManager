using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.TencentCloud;

public static class TencentCloudConfigurationExtensions
{
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
