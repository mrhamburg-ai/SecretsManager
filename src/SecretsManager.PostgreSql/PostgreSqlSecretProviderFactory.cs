using SecretsManager.Builder;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Factory that creates <see cref="PostgreSqlSecretProvider"/> instances from a flat settings dictionary.
/// </summary>
public sealed class PostgreSqlSecretProviderFactory : ISecretProviderFactory
{
    private readonly PostgreSqlOptions? _capturedOptions;

    /// <summary>
    /// Initializes a factory that builds its options purely from the settings dictionary. Used when
    /// configuration comes from YAML, environment variables, or <c>IConfiguration</c>.
    /// </summary>
    public PostgreSqlSecretProviderFactory()
    {
    }

    /// <summary>
    /// Initializes a factory that starts from options already configured through the fluent API.
    /// </summary>
    /// <param name="capturedOptions">
    /// The options produced by the <c>WithPostgreSql</c> configuration action. They are used as the
    /// base, and the string settings override the values that are expressible as strings.
    /// </param>
    /// <remarks>
    /// This overload exists because <see cref="ISecretProviderFactory.Create"/> accepts only strings.
    /// Without it, an object-valued option such as
    /// <see cref="PostgreSqlOptions.EncryptionKeyProvider"/> is silently dropped, the provider finds
    /// no key, and it falls back to writing plaintext.
    /// </remarks>
    public PostgreSqlSecretProviderFactory(PostgreSqlOptions capturedOptions)
    {
        ArgumentNullException.ThrowIfNull(capturedOptions);
        _capturedOptions = capturedOptions;
    }

    /// <summary>
    /// The provider name used in YAML/env configuration: <c>"postgresql"</c>.
    /// </summary>
    public string ProviderName => "postgresql";

    /// <summary>
    /// Creates a <see cref="PostgreSqlSecretProvider"/> from the flat settings dictionary.
    /// Recognized keys: <c>connection.string</c>, <c>schema</c>, <c>schema.owner</c>,
    /// <c>table.prefix</c>, <c>auto.create.schema</c>, <c>encryption.key</c>, <c>encryption.keyfile</c>.
    /// </summary>
    /// <param name="configuration">The flat settings dictionary.</param>
    /// <returns>A configured <see cref="PostgreSqlSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        // Clone rather than merge into the captured instance: Build() may be called more than once
        // on one builder, and each call would otherwise overwrite the previous one's settings.
        var options = MapConfiguration(configuration, Clone(_capturedOptions));
        return new PostgreSqlSecretProvider(options);
    }

    /// <summary>
    /// Produces a shallow copy of the supplied options, or fresh defaults when there are none.
    /// </summary>
    /// <param name="source">The options configured through the fluent API, or null.</param>
    /// <returns>A copy that the caller may freely mutate.</returns>
    private static PostgreSqlOptions Clone(PostgreSqlOptions? source)
    {
        if (source is null)
            return new PostgreSqlOptions();

        return new PostgreSqlOptions
        {
            ConnectionString = source.ConnectionString,
            Schema = source.Schema,
            TablePrefix = source.TablePrefix,
            AutoCreateSchema = source.AutoCreateSchema,
            SchemaOwner = source.SchemaOwner,
            EncryptionKey = source.EncryptionKey,
            EncryptionKeyFile = source.EncryptionKeyFile,

            // An object, so it is shared rather than copied: the key provider is a stateless reader
            // and the caller owns its lifetime.
            EncryptionKeyProvider = source.EncryptionKeyProvider,
            LegacyEncryptionKey = source.LegacyEncryptionKey
        };
    }

    /// <param name="configuration">The flat settings dictionary.</param>
    /// <param name="into">
    /// When supplied, the settings are merged into this instance instead of a fresh one. Used to
    /// carry object-valued options that cannot travel through the string dictionary.
    /// </param>
    internal static PostgreSqlOptions MapConfiguration(
        IReadOnlyDictionary<string, string> configuration, PostgreSqlOptions? into = null)
    {
        var options = into ?? new PostgreSqlOptions();

        if (configuration.TryGetValue("connection.string", out var cs) && !string.IsNullOrWhiteSpace(cs))
            options.ConnectionString = cs;

        if (configuration.TryGetValue("schema", out var schema) && !string.IsNullOrWhiteSpace(schema))
            options.Schema = schema;

        if (configuration.TryGetValue("table.prefix", out var prefix) && !string.IsNullOrWhiteSpace(prefix))
            options.TablePrefix = prefix;

        if (configuration.TryGetValue("auto.create.schema", out var autoCreate) && !string.IsNullOrWhiteSpace(autoCreate))
            options.AutoCreateSchema = bool.Parse(autoCreate);

        if (configuration.TryGetValue("schema.owner", out var schemaOwner) && !string.IsNullOrWhiteSpace(schemaOwner))
            options.SchemaOwner = schemaOwner;

        if (configuration.TryGetValue("encryption.key", out var encKey) && !string.IsNullOrWhiteSpace(encKey))
            options.EncryptionKey = encKey;

        if (configuration.TryGetValue("encryption.keyfile", out var encKeyFile) && !string.IsNullOrWhiteSpace(encKeyFile))
            options.EncryptionKeyFile = encKeyFile;

        return options;
    }
}
