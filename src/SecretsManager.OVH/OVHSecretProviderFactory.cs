using SecretsManager.Builder;

namespace SecretsManager.OVH;

/// <summary>
/// A factory that creates <see cref="OVHSecretProvider"/> instances from a flat configuration dictionary.
/// </summary>
public sealed class OVHSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// Gets the provider name used in YAML or environment configuration: <c>ovh</c>.
    /// </summary>
    public string ProviderName => "ovh";

    /// <summary>
    /// Creates an <see cref="OVHSecretProvider"/> from the specified configuration dictionary.
    /// </summary>
    /// <param name="configuration">A dictionary of setting keys and values. Recognized keys: <c>endpoint</c>, <c>application.key</c>, <c>application.secret</c>, <c>consumer.key</c>, and <c>okms.id</c>.</param>
    /// <returns>A configured <see cref="OVHSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new OVHSecretProvider(options);
    }

    internal static OVHOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new OVHOptions();

        if (configuration.TryGetValue("endpoint", out var endpoint) && !string.IsNullOrWhiteSpace(endpoint))
            options.Endpoint = endpoint;

        if (configuration.TryGetValue("application.key", out var appKey) && !string.IsNullOrWhiteSpace(appKey))
            options.ApplicationKey = appKey;

        if (configuration.TryGetValue("application.secret", out var appSecret) && !string.IsNullOrWhiteSpace(appSecret))
            options.ApplicationSecret = appSecret;

        if (configuration.TryGetValue("consumer.key", out var consumerKey) && !string.IsNullOrWhiteSpace(consumerKey))
            options.ConsumerKey = consumerKey;

        if (configuration.TryGetValue("okms.id", out var okmsId) && !string.IsNullOrWhiteSpace(okmsId))
            options.OkmsId = okmsId;

        return options;
    }
}
