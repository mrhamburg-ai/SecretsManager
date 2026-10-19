using SecretsManager.Internal;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Configuration options for the PostgreSQL secret provider.
/// </summary>
public sealed class PostgreSqlOptions
{
    /// <summary>
    /// PostgreSQL connection string (required).
    /// Example: "Host=localhost;Database=secrets;Username=app;Password=pass"
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Database schema for the tables. Defaults to "public".
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Prefix for table names to avoid collisions. Defaults to "sm_".
    /// </summary>
    public string TablePrefix { get; set; } = "sm_";

    /// <summary>
    /// Whether to automatically create tables on first use. Defaults to false: prefer creating them
    /// from a migration role so the runtime account does not need CREATE rights on the schema.
    /// </summary>
    public bool AutoCreateSchema { get; set; }

    /// <summary>
    /// Role expected to own the secrets tables. When null, the connected role must own them. Set this
    /// when a migration role creates the tables and the application connects as a different,
    /// lower-privileged role.
    /// </summary>
    public string? SchemaOwner { get; set; }

    /// <summary>
    /// Base64-encoded AES-256 encryption key (32 bytes).
    /// When set, secret values are encrypted at rest using AES-256-GCM.
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Path to a file containing the encryption key.
    /// If the file is exactly 32 bytes it is used as-is; otherwise it is treated as base64.
    /// </summary>
    public string? EncryptionKeyFile { get; set; }

    /// <summary>
    /// Supplies the encryption key from an external source, for example another secret provider.
    /// Mutually exclusive with <see cref="EncryptionKey"/> and <see cref="EncryptionKeyFile"/> for
    /// new writes. Because this is an object it cannot come from appsettings.json, YAML or
    /// environment variables — set it through the fluent API.
    /// </summary>
    public IEncryptionKeyProvider? EncryptionKeyProvider { get; set; }

    /// <summary>
    /// Base64-encoded key used only to <em>decrypt</em> values written before key ids were recorded.
    /// Set this when migrating an existing database to <see cref="EncryptionKeyProvider"/>.
    /// </summary>
    public string? LegacyEncryptionKey { get; set; }

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// An empty collection indicates the options are valid.
    /// </summary>
    /// <returns>Validation error messages; empty when the options are valid.</returns>
    /// <summary>
    /// PostgreSQL truncates identifiers at 63 bytes. The longest name this provider builds is
    /// "{prefix}secret_versions", so the prefix must leave room for that suffix or the two table
    /// names can collapse into the same identifier.
    /// </summary>
    private const int MaxTablePrefixLength = 40;

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// An empty collection indicates the options are valid.
    /// </summary>
    /// <returns>Validation error messages; empty when the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            yield return "ConnectionString is required";

        if (!string.IsNullOrEmpty(EncryptionKey) && !string.IsNullOrEmpty(EncryptionKeyFile))
            yield return "EncryptionKey and EncryptionKeyFile are mutually exclusive";

        var writeSources = new[]
        {
            EncryptionKey is not null,
            EncryptionKeyFile is not null,
            EncryptionKeyProvider is not null
        };
        if (writeSources.Count(x => x) > 1)
            yield return "EncryptionKey, EncryptionKeyFile and EncryptionKeyProvider are mutually exclusive";

        if (LegacyEncryptionKey is not null && EncryptionKeyProvider is null)
            yield return "LegacyEncryptionKey is only meaningful alongside EncryptionKeyProvider";

        if (TablePrefix.Length > MaxTablePrefixLength)
            yield return $"TablePrefix must be {MaxTablePrefixLength} characters or fewer";
    }
}
