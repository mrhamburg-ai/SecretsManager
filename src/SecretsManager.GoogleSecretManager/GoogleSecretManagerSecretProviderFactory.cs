using SecretsManager.Builder;

namespace SecretsManager.GoogleSecretManager;

/// <summary>
/// Factory that creates a <see cref="GoogleSecretManagerSecretProvider"/> from a flat configuration dictionary.
/// Used by <see cref="SecretProviderBuilder"/> when the "googlesecretmanager" provider is selected.
/// </summary>
public sealed class GoogleSecretManagerSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// Gets the provider name used in YAML and environment configuration.
    /// </summary>
    public string ProviderName => "googlesecretmanager";

    /// <summary>
    /// Creates a <see cref="GoogleSecretManagerSecretProvider"/> from a flat configuration dictionary.
    /// </summary>
    /// <param name="configuration">The configuration dictionary containing the keys <c>project.id</c>, <c>credentials.path</c>, <c>credentials.json</c>, and <c>endpoint</c>.</param>
    /// <returns>The Google Secret Manager secret provider.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new GoogleSecretManagerSecretProvider(options);
    }

    internal static GoogleSecretManagerOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new GoogleSecretManagerOptions();

        if (configuration.TryGetValue("project.id", out var projectId) && !string.IsNullOrWhiteSpace(projectId))
            options.ProjectId = projectId;

        if (configuration.TryGetValue("credentials.path", out var credentialsPath) && !string.IsNullOrWhiteSpace(credentialsPath))
            options.CredentialsPath = credentialsPath;

        if (configuration.TryGetValue("credentials.json", out var credentialsJson) && !string.IsNullOrWhiteSpace(credentialsJson))
            options.CredentialsJson = credentialsJson;

        if (configuration.TryGetValue("endpoint", out var endpoint) && !string.IsNullOrWhiteSpace(endpoint))
            options.Endpoint = endpoint;

        return options;
    }
}
