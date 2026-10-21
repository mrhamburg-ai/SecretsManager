using SecretsManager.PostgreSql.Internal;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Writes audit records to a PostgreSQL table. The sink never stores secret values.
/// </summary>
public sealed class PostgreSqlSecretAuditSink : ISecretAuditSink
{
    private readonly IPostgreSqlAuditStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlSecretAuditSink"/> class.
    /// </summary>
    /// <param name="options">The connection, schema, and table options.</param>
    /// <exception cref="ArgumentException">The options are invalid.</exception>
    public PostgreSqlSecretAuditSink(PostgreSqlAuditSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = new PostgreSqlAuditStore(options);
    }

    internal PostgreSqlSecretAuditSink(IPostgreSqlAuditStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        return _store.WriteAsync(auditEvent, cancellationToken);
    }
}
