using SecretsManager.Builder;

namespace SecretsManager.Infisical;

/// <summary>
/// Factory that creates an <see cref="InfisicalSecretProvider"/> from a flat configuration
/// dictionary. Used by <see cref="SecretProviderBuilder"/> when the <c>infisical</c> provider is
/// selected.
/// </summary>
public sealed class InfisicalSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML, environment variables and the builder API: <c>infisical</c>.
    /// </summary>
    public string ProviderName => "infisical";

    /// <summary>
    /// Creates an <see cref="InfisicalSecretProvider"/> from the given settings, reading the
    /// <c>host.uri</c>, <c>auth.client.id</c>, <c>auth.client.secret</c>, <c>project.id</c>,
    /// <c>environment</c>, <c>secret.path</c> and <c>expand.secret.references</c> keys.
    /// </summary>
    /// <param name="configuration">Flat provider settings.</param>
    /// <returns>The configured provider.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <c>expand.secret.references</c> is not <c>true</c> or <c>false</c>.
    /// </exception>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new InfisicalSecretProvider(options);
    }

    internal static InfisicalOptions MapConfiguration(
        IReadOnlyDictionary<string, string> configuration)
    {
        var options = new InfisicalOptions();

        if (configuration.TryGetValue("host.uri", out var hostUri)
            && !string.IsNullOrWhiteSpace(hostUri))
            options.HostUri = hostUri;

        if (configuration.TryGetValue("auth.client.id", out var clientId)
            && !string.IsNullOrWhiteSpace(clientId))
            options.ClientId = clientId;

        if (configuration.TryGetValue("auth.client.secret", out var clientSecret)
            && !string.IsNullOrWhiteSpace(clientSecret))
            options.ClientSecret = clientSecret;

        if (configuration.TryGetValue("project.id", out var projectId)
            && !string.IsNullOrWhiteSpace(projectId))
            options.ProjectId = projectId;

        if (configuration.TryGetValue("environment", out var environment)
            && !string.IsNullOrWhiteSpace(environment))
            options.EnvironmentSlug = environment;

        if (configuration.TryGetValue("secret.path", out var secretPath)
            && !string.IsNullOrWhiteSpace(secretPath))
            options.SecretPath = secretPath;

        if (configuration.TryGetValue("expand.secret.references", out var expand)
            && !string.IsNullOrWhiteSpace(expand))
        {
            if (!bool.TryParse(expand, out var parsed))
                throw new ArgumentException(
                    $"Invalid value '{expand}' for expand.secret.references. Use 'true' or 'false'.");

            options.ExpandSecretReferences = parsed;
        }

        return options;
    }
}
