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
    private Func<ISecretProvider>? _providerFactory;

    /// <summary>
    /// Registers a provider factory so its <see cref="ISecretProviderFactory.ProviderName"/>
    /// can be resolved at build time.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a factory with the same provider name is already registered.
    /// </exception>
    public SecretProviderBuilder RegisterProvider(ISecretProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(factory.ProviderName, factory))
            throw new InvalidOperationException(
                $"A provider factory named '{factory.ProviderName}' is already registered. " +
                "Each provider name may be registered only once.");

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
        _providerFactory = null;
        configure?.Invoke(_configuration.Settings);
        return this;
    }

    /// <summary>
    /// Selects a provider by name and supplies an already constructed provider.
    /// Use this overload when the options cannot be represented as a flat settings
    /// dictionary, for example when they contain delegates or object-valued settings.
    /// </summary>
    /// <param name="providerName">The provider name reported by the layer pipeline.</param>
    /// <param name="providerFactory">Creates the provider when <see cref="Build"/> runs.</param>
    /// <returns>The same builder for method chaining.</returns>
    public SecretProviderBuilder UseProvider(string providerName, Func<ISecretProvider> providerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(providerFactory);

        _configuration = new SecretProviderConfiguration { ProviderName = providerName };
        _providerFactory = providerFactory;
        return this;
    }

    /// <summary>
    /// Loads configuration from environment variables.
    /// Reads {prefix}_PROVIDER for the provider name, then {prefix}_{PROVIDER}_{KEY} for settings.
    /// </summary>
    public SecretProviderBuilder FromEnvironment(string prefix = "SECRETS")
    {
        _configuration = EnvironmentVariableConfigurationReader.Read(prefix);
        _providerFactory = null;
        return this;
    }

    /// <summary>
    /// Loads configuration from a YAML file.
    /// </summary>
    public SecretProviderBuilder FromYamlFile(string path)
    {
        var yaml = File.ReadAllText(path);
        _configuration = YamlConfigurationReader.Read(yaml);
        _providerFactory = null;
        return this;
    }

    /// <summary>
    /// Loads configuration from a YAML string.
    /// </summary>
    public SecretProviderBuilder FromYaml(string yaml)
    {
        _configuration = YamlConfigurationReader.Read(yaml);
        _providerFactory = null;
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

        ISecretProvider provider;
        if (_providerFactory is not null)
        {
            provider = _providerFactory()
                ?? throw new InvalidOperationException(
                    $"The provider factory supplied for '{_configuration.ProviderName}' returned null.");
        }
        else
        {
            if (!_factories.TryGetValue(_configuration.ProviderName, out var factory))
                throw new InvalidOperationException(
                    $"No factory registered for provider '{_configuration.ProviderName}'. " +
                    $"Registered providers: {string.Join(", ", _factories.Keys)}. " +
                    "Ensure you have called the provider's registration extension (e.g. WithFileSystem()) before Build().");

            provider = factory.Create(_configuration.Settings);
        }

        var logger = _layers
            .OfType<LoggingLayer>()
            .Select(layer => layer.Options.Logger)
            .FirstOrDefault();

        if (logger is not null)
        {
            foreach (var layer in _layers.OfType<ISecretProviderLoggerAwareLayer>())
                layer.SetLogger(logger);
        }

        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            if (_layers[i] is IProviderNameAwareLayer aware)
                aware.SetProviderName(_configuration.ProviderName);

            provider = _layers[i].Wrap(provider);
        }

        return provider;
    }
}
