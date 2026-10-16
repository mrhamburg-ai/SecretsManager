using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.OVH;

public static class OVHConfigurationExtensions
{
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
