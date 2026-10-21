using Npgsql;
using NpgsqlTypes;

namespace SecretsManager.PostgreSql.Internal;

internal sealed class PostgreSqlAuditStore : IPostgreSqlAuditStore
{
    private readonly string _connectionString;
    private readonly string _schema;
    private readonly string _tableName;
    private readonly bool _autoCreateSchema;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaEnsured;

    private string Table => $"\"{_schema}\".\"{_tableName}\"";

    public PostgreSqlAuditStore(PostgreSqlAuditSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Invalid {nameof(PostgreSqlAuditSinkOptions)}: {string.Join("; ", errors)}",
                nameof(options));

        _connectionString = options.ConnectionString;
        _schema = options.Schema;
        _tableName = options.TableName;
        _autoCreateSchema = options.AutoCreateSchema;
    }

    public async ValueTask WriteAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"""
            INSERT INTO {Table} (
                "timestamp", "operation", "provider", "key", "version", "outcome",
                "duration_ms", "actor_id", "actor_display_name", "correlation_id",
                "trace_id", "failure_type")
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = auditEvent.Timestamp });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = auditEvent.Operation });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = auditEvent.Provider });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.Key ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.Version ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = auditEvent.Outcome.ToString() });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Double, Value = auditEvent.Duration.TotalMilliseconds });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.ActorId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.ActorDisplayName ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.CorrelationId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.TraceId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)auditEvent.FailureType ?? DBNull.Value });

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured || !_autoCreateSchema)
            return;

        await _schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaEnsured)
                return;

            var indexName = $"{_tableName}_timestamp_idx";
            var ddl = $"""
                CREATE TABLE IF NOT EXISTS {Table} (
                    id                  BIGSERIAL PRIMARY KEY,
                    "timestamp"         TIMESTAMPTZ NOT NULL,
                    "operation"         TEXT NOT NULL,
                    "provider"          TEXT NOT NULL,
                    "key"               TEXT,
                    "version"           TEXT,
                    "outcome"           TEXT NOT NULL,
                    "duration_ms"       DOUBLE PRECISION NOT NULL,
                    "actor_id"          TEXT,
                    "actor_display_name" TEXT,
                    "correlation_id"    TEXT,
                    "trace_id"          TEXT,
                    "failure_type"      TEXT
                );

                CREATE INDEX IF NOT EXISTS "{indexName}"
                    ON {Table} ("timestamp");
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _schemaEnsured = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
