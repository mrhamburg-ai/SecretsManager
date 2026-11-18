using SecretsManager.Builder;

namespace SecretsManager.Doppler;

/// <summary>
/// Factory that creates a <see cref="DopplerSecretProvider"/> from a flat configuration
/// dictionary. Used by <see cref="SecretProviderBuilder"/> when the <c>doppler</c> provider is
/// selected.
/// </summary>
public sealed class DopplerSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML, environment variables and the builder API: <c>doppler</c>.
    /// </summary>
    public string ProviderName => "doppler";

    /// <summary>
    /// Creates a <see cref="DopplerSecretProvider"/> from the given settings, reading the
    /// <c>api.url</c>, <c>token</c>, <c>project</c>, <c>config</c> and <c>use.raw.values</c> keys.
    /// </summary>
    /// <param name="configuration">Flat provider settings.</param>
    /// <returns>The configured provider.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <c>use.raw.values</c> is not <c>true</c> or <c>false</c>.
    /// </exception>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new DopplerSecretProvider(options);
    }

    internal static DopplerOptions MapConfiguration(
        IReadOnlyDictionary<string, string> configuration)
    {
        var options = new DopplerOptions();

        if (configuration.TryGetValue("api.url", out var apiUrl)
            && !string.IsNullOrWhiteSpace(apiUrl))
            options.ApiUrl = apiUrl;

        if (configuration.TryGetValue("token", out var token)
            && !string.IsNullOrWhiteSpace(token))
            options.Token = token;

        if (configuration.TryGetValue("project", out var project)
            && !string.IsNullOrWhiteSpace(project))
            options.Project = project;

        if (configuration.TryGetValue("config", out var config)
            && !string.IsNullOrWhiteSpace(config))
            options.Config = config;

        if (configuration.TryGetValue("use.raw.values", out var raw)
            && !string.IsNullOrWhiteSpace(raw))
        {
            if (!bool.TryParse(raw, out var parsed))
                throw new ArgumentException(
                    $"Invalid value '{raw}' for use.raw.values. Use 'true' or 'false'.");

            options.UseRawValues = parsed;
        }

        return options;
    }
}
