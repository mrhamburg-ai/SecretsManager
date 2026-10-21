using SecretsManager.Internal;

namespace SecretsManager.Builder;

/// <summary>
/// Builds an <see cref="ISecretProvider"/> from fluent API calls, environment variables, or YAML configuration.
/// Provider packages register themselves via extension methods that call <see cref="RegisterProvider"/>.
/// </summary>
public sealed class SecretProviderBuilder
{
    private readonly Dictionary<string, ISecretProviderFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ISecretProviderLayer> _layers = [];
    private SecretProviderConfiguration? _configuration;

    /// <summary>
    /// Registers a provider factory so its <see cref="ISecretProviderFactory.ProviderName"/>
    /// can be resolved at build time.
    /// </summary>
    public SecretProviderBuilder RegisterProvider(ISecretProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories[factory.ProviderName] = factory;
        return this;
    }

    /// <summary>
    /// Adds a layer that wraps the provider implementation.
    /// Layers are applied in registration order, so operations pass through earlier layers first.
    /// </summary>
    public SecretProviderBuilder AddLayer(ISecretProviderLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layers.Add(layer);
        return this;
    }

    internal TLayer GetOrAddLayer<TLayer>(Func<TLayer> factory)
        where TLayer : class, ISecretProviderLayer
    {
        foreach (var existing in _layers)
        {
            if (existing is TLayer layer)
                return layer;
        }

        var created = factory();
        _layers.Add(created);
        return created;
    }

    /// <summary>
    /// Selects a provider by name and configures it via a settings dictionary.
    /// </summary>
    public SecretProviderBuilder UseProvider(string providerName, Action<Dictionary<string, string>>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        _configuration = new SecretProviderConfiguration { ProviderName = providerName };
        configure?.Invoke(_configuration.Settings);
        return this;
    }

    /// <summary>
    /// Loads configuration from environment variables.
    /// Reads {prefix}_PROVIDER for the provider name, then {prefix}_{PROVIDER}_{KEY} for settings.
    /// </summary>
    public SecretProviderBuilder FromEnvironment(string prefix = "SECRETS")
    {
        _configuration = EnvironmentVariableConfigurationReader.Read(prefix);
        return this;
    }

    /// <summary>
    /// Loads configuration from a YAML file.
    /// </summary>
    public SecretProviderBuilder FromYamlFile(string path)
    {
        var yaml = File.ReadAllText(path);
        _configuration = YamlConfigurationReader.Read(yaml);
        return this;
    }

    /// <summary>
    /// Loads configuration from a YAML string.
    /// </summary>
    public SecretProviderBuilder FromYaml(string yaml)
    {
        _configuration = YamlConfigurationReader.Read(yaml);
        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="ISecretProvider"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no configuration has been set or the provider name is not registered.
    /// </exception>
    public ISecretProvider Build()
    {
        if (_configuration is null || string.IsNullOrWhiteSpace(_configuration.ProviderName))
            throw new InvalidOperationException(
                "No provider configured. Call UseProvider(), FromEnvironment(), FromYaml(), or FromYamlFile() before Build().");

        if (!_factories.TryGetValue(_configuration.ProviderName, out var factory))
            throw new InvalidOperationException(
                $"No factory registered for provider '{_configuration.ProviderName}'. " +
                $"Registered providers: {string.Join(", ", _factories.Keys)}. " +
                "Ensure you have called the provider's registration extension (e.g. WithFileSystem()) before Build().");

        var provider = factory.Create(_configuration.Settings);

        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            if (_layers[i] is IProviderNameAwareLayer aware)
                aware.SetProviderName(_configuration.ProviderName);

            provider = _layers[i].Wrap(provider);
        }

        return provider;
    }
}
