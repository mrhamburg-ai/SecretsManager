using SecretsManager.Builder;

namespace SecretsManager.TencentCloud;

/// <summary>
/// Factory that creates <see cref="TencentCloudSecretProvider"/> instances from a flat settings dictionary.
/// </summary>
public sealed class TencentCloudSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML or environment configuration.
    /// </summary>
    public string ProviderName => "tencentCloud";

    /// <summary>
    /// Creates a <see cref="TencentCloudSecretProvider"/> from the provided configuration settings.
    /// </summary>
    /// <param name="configuration">A dictionary of settings with keys: <c>region</c>, <c>secret.id</c>, <c>secret.key</c>, and optionally <c>endpoint</c>.</param>
    /// <returns>A configured <see cref="TencentCloudSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new TencentCloudSecretProvider(options);
    }

    internal static TencentCloudOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new TencentCloudOptions();

        if (configuration.TryGetValue("region", out var region) && !string.IsNullOrWhiteSpace(region))
            options.Region = region;

        if (configuration.TryGetValue("secret.id", out var secretId) && !string.IsNullOrWhiteSpace(secretId))
            options.SecretId = secretId;

        if (configuration.TryGetValue("secret.key", out var secretKey) && !string.IsNullOrWhiteSpace(secretKey))
            options.SecretKey = secretKey;

        if (configuration.TryGetValue("endpoint", out var endpoint) && !string.IsNullOrWhiteSpace(endpoint))
            options.Endpoint = endpoint;

        return options;
    }
}
