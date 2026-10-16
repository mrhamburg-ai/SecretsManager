using SecretsManager.Builder;

namespace SecretsManager.Scaleway;

/// <summary>
/// Factory that creates <see cref="ScalewaySecretProvider"/> instances from a flat settings dictionary.
/// </summary>
public sealed class ScalewaySecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML/env configuration: <c>"scaleway"</c>.
    /// </summary>
    public string ProviderName => "scaleway";

    /// <summary>
    /// Creates a <see cref="ScalewaySecretProvider"/> from the flat settings dictionary.
    /// Recognized keys: <c>region</c>, <c>project.id</c>, <c>access.key</c>, <c>secret.key</c>.
    /// </summary>
    /// <param name="configuration">The flat settings dictionary.</param>
    /// <returns>A configured <see cref="ScalewaySecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new ScalewaySecretProvider(options);
    }

    internal static ScalewayOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new ScalewayOptions();

        if (configuration.TryGetValue("region", out var region) && !string.IsNullOrWhiteSpace(region))
            options.Region = region;

        if (configuration.TryGetValue("project.id", out var projectId) && !string.IsNullOrWhiteSpace(projectId))
            options.ProjectId = projectId;

        if (configuration.TryGetValue("access.key", out var accessKey) && !string.IsNullOrWhiteSpace(accessKey))
            options.AccessKey = accessKey;

        if (configuration.TryGetValue("secret.key", out var secretKey) && !string.IsNullOrWhiteSpace(secretKey))
            options.SecretKey = secretKey;

        return options;
    }
}
