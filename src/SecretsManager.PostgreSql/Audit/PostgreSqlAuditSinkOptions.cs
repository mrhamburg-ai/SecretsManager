using System.Text.RegularExpressions;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Options for <see cref="PostgreSqlSecretAuditSink"/>.
/// </summary>
public sealed partial class PostgreSqlAuditSinkOptions
{
    /// <summary>
    /// PostgreSQL connection string (required).
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Database schema for the audit table. Defaults to <c>public</c>.
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Audit table name. Defaults to <c>sm_secret_audit_log</c>. Limited to 48 characters so the
    /// generated <c>&lt;table&gt;_timestamp_idx</c> index name stays within PostgreSQL's 63-character
    /// identifier limit.
    /// </summary>
    public string TableName { get; set; } = "sm_secret_audit_log";

    /// <summary>
    /// Whether to create the audit table on first write. Defaults to <c>false</c> so the runtime
    /// account does not need <c>CREATE</c> rights on the schema.
    /// </summary>
    public bool AutoCreateSchema { get; set; }

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// </summary>
    /// <returns>Validation error messages; empty when the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            yield return "ConnectionString is required";

        if (string.IsNullOrWhiteSpace(Schema))
            yield return "Schema is required";
        else if (!SafeIdentifierRegex().IsMatch(Schema))
            yield return "Schema must contain only letters, digits, or underscores and start with a letter or underscore";

        if (string.IsNullOrWhiteSpace(TableName))
            yield return "TableName is required";
        else if (!SafeIdentifierRegex().IsMatch(TableName))
            yield return "TableName must contain only letters, digits, or underscores and start with a letter or underscore";
        else if (TableName.Length > 48)
            yield return "TableName must be 48 characters or fewer so the generated index name fits";
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex SafeIdentifierRegex();
}
