using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.AliyunKms;

/// <summary>
/// Configuration bridge for [A-Zaaz] provider.
/// </summary>
public static class AliyunKmsConfigurationExtensions
{
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
