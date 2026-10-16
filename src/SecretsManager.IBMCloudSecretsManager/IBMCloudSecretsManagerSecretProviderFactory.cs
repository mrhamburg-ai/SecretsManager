using SecretsManager.Builder;

namespace SecretsManager.IBMCloudSecretsManager;

/// <summary>
/// A factory that creates <see cref="IBMCloudSecretsManagerSecretProvider"/> instances from a flat configuration dictionary.
/// </summary>
public sealed class IBMCloudSecretsManagerSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// Gets the provider name used in YAML or environment configuration: <c>ibmcloud-secrets-manager</c>.
    /// </summary>
    public string ProviderName => "ibmcloud-secrets-manager";

    /// <summary>
    /// Creates an <see cref="IBMCloudSecretsManagerSecretProvider"/> from the specified configuration dictionary.
    /// </summary>
    /// <param name="configuration">A dictionary of setting keys and values. Recognized keys: <c>region</c>, <c>instance.id</c>, <c>api.key</c>, and <c>service.url</c>.</param>
    /// <returns>A configured <see cref="IBMCloudSecretsManagerSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new IBMCloudSecretsManagerSecretProvider(options);
    }

    internal static IBMCloudSecretsManagerOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new IBMCloudSecretsManagerOptions();

        if (configuration.TryGetValue("region", out var region) && !string.IsNullOrWhiteSpace(region))
            options.Region = region;

        if (configuration.TryGetValue("instance.id", out var instanceId) && !string.IsNullOrWhiteSpace(instanceId))
            options.InstanceId = instanceId;

        if (configuration.TryGetValue("api.key", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
            options.ApiKey = apiKey;

        if (configuration.TryGetValue("service.url", out var serviceUrl) && !string.IsNullOrWhiteSpace(serviceUrl))
            options.ServiceUrl = serviceUrl;

        return options;
    }
}
