namespace SecretsManager.PostgreSql;

public sealed class PostgreSqlOptions
{
    public string ConnectionString { get; set; } = "";
    public string Schema { get; set; } = "public";
    public string TablePrefix { get; set; } = "sm_";
    public bool AutoCreateSchema { get; set; } = true;
    public string? EncryptionKey { get; set; }
    public string? EncryptionKeyFile { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            yield return "ConnectionString is required";
    }
}
