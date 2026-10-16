using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Scaleway;

/// <summary>
/// Configuration bridge for [A-Zaaz] provider.
/// </summary>
public static class ScalewayConfigurationExtensions
{
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
