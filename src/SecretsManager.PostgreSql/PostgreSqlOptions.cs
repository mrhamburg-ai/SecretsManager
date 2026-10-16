namespace SecretsManager.PostgreSql;

public sealed class PostgreSqlOptions
{
    /// <summary>
    /// PostgreSQL connection string (required)
    /// </summary>
    public string ConnectionString { get; set; } = "";
    /// <summary>
    /// Database schema for the tables. Defaults to "public"
    /// </summary>
    public string Schema { get; set; } = "public";
    /// <summary>
    /// Prefix for table names to avoid collisions. Defaults to "sm_"
    /// </summary>
    public string TablePrefix { get; set; } = "sm_";
    /// <summary>
    /// Database schema for the tables. Defaults to "public"
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;
    /// <summary>
    /// Base64-encoded AES-256 encryption key (32 bytes)
    /// </summary>
    public string? EncryptionKey { get; set; }
    /// <summary>
    /// Path to a file containing the encryption key
    /// </summary>
    public string? EncryptionKeyFile { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            yield return "ConnectionString is required";
    }
}
