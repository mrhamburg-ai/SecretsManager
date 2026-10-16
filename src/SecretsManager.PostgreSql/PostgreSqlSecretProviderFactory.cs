using SecretsManager.Builder;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Factory that creates <see cref="PostgreSqlSecretProvider"/> instances from a flat settings dictionary.
/// </summary>
public sealed class PostgreSqlSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML/env configuration: <c>"postgresql"</c>.
    /// </summary>
    public string ProviderName => "postgresql";

    /// <summary>
    /// Creates a <see cref="PostgreSqlSecretProvider"/> from the flat settings dictionary.
    /// Recognized keys: <c>connection.string</c>, <c>schema</c>, <c>table.prefix</c>,
    /// <c>auto.create.schema</c>, <c>encryption.key</c>, <c>encryption.keyfile</c>.
    /// </summary>
    /// <param name="configuration">The flat settings dictionary.</param>
    /// <returns>A configured <see cref="PostgreSqlSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new PostgreSqlSecretProvider(options);
    }

    internal static PostgreSqlOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new PostgreSqlOptions();

        if (configuration.TryGetValue("connection.string", out var cs) && !string.IsNullOrWhiteSpace(cs))
            options.ConnectionString = cs;

        if (configuration.TryGetValue("schema", out var schema) && !string.IsNullOrWhiteSpace(schema))
            options.Schema = schema;

        if (configuration.TryGetValue("table.prefix", out var prefix) && !string.IsNullOrWhiteSpace(prefix))
            options.TablePrefix = prefix;

        if (configuration.TryGetValue("auto.create.schema", out var autoCreate) && !string.IsNullOrWhiteSpace(autoCreate))
            options.AutoCreateSchema = bool.Parse(autoCreate);

        if (configuration.TryGetValue("encryption.key", out var encKey) && !string.IsNullOrWhiteSpace(encKey))
            options.EncryptionKey = encKey;

        if (configuration.TryGetValue("encryption.keyfile", out var encKeyFile) && !string.IsNullOrWhiteSpace(encKeyFile))
            options.EncryptionKeyFile = encKeyFile;

        return options;
    }
}
