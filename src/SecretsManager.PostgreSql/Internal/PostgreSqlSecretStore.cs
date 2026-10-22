using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace SecretsManager.PostgreSql.Internal;

internal sealed partial class PostgreSqlSecretStore : IPostgreSqlSecretStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schema;
    private readonly string _tablePrefix;
    private readonly bool _autoCreateSchema;
    private readonly string? _schemaOwner;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaEnsured;

    private string SecretsTable => $"\"{_schema}\".\"{_tablePrefix}secrets\"";
    private string VersionsTable => $"\"{_schema}\".\"{_tablePrefix}secret_versions\"";

    public PostgreSqlSecretStore(PostgreSqlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("ConnectionString is required.", nameof(options));

        ValidateIdentifier(options.Schema, "Schema");
        ValidateIdentifier(options.TablePrefix, "TablePrefix");

        _schema = options.Schema;
        _tablePrefix = options.TablePrefix;
        _autoCreateSchema = options.AutoCreateSchema;
        _schemaOwner = options.SchemaOwner;
        _dataSource = NpgsqlDataSource.Create(options.ConnectionString);
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _schemaEnsured) || !_autoCreateSchema) return;

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _schemaEnsured)) return;

            var ddl = $"""
                CREATE TABLE IF NOT EXISTS {SecretsTable} (
                    id          SERIAL PRIMARY KEY,
                    key         TEXT NOT NULL UNIQUE,
                    content_type TEXT,
                    tags        JSONB,
                    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS {VersionsTable} (
                    id          SERIAL PRIMARY KEY,
                    secret_id   INTEGER NOT NULL REFERENCES {SecretsTable}(id) ON DELETE CASCADE,
                    version     INTEGER NOT NULL,
                    value       TEXT NOT NULL,
                    encrypted   BOOLEAN NOT NULL DEFAULT false,
                    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                    is_current  BOOLEAN NOT NULL DEFAULT false,
                    UNIQUE(secret_id, version)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "{_tablePrefix}secret_versions_current_idx"
                    ON {VersionsTable} (secret_id) WHERE is_current = true;
                """;

            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await VerifyTableOwnershipAsync(conn, cancellationToken);

            Volatile.Write(ref _schemaEnsured, true);
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    /// <summary>
    /// Confirms the application owns the tables it is about to use. A role that can create objects in
    /// the target schema could otherwise create the tables first, own them, and then read every secret
    /// written to them afterwards — or add a trigger that copies values elsewhere.
    /// </summary>
    private async Task VerifyTableOwnershipAsync(NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.relname, pg_get_userbyid(c.relowner) AS owner, current_user AS connected_role
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = $1 AND c.relname = ANY($2)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = _schema });
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = new[] { $"{_tablePrefix}secrets", $"{_tablePrefix}secret_versions" }
        });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            var owner = reader.GetString(1);
            var connected = reader.GetString(2);
            var expected = _schemaOwner ?? connected;

            if (!string.Equals(owner, expected, StringComparison.Ordinal))
                throw new SecretProviderException(
                    $"Table '{_schema}.{table}' is owned by '{owner}', but '{expected}' was expected. " +
                    "Refusing to use tables this application does not own — they may have been created " +
                    "by another role. Set PostgreSqlOptions.SchemaOwner if a separate migration role " +
                    "owns the tables.");
        }
    }

    public async Task<PostgreSqlSecretRow?> GetSecretByKeyAsync(string key, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var sql = $"SELECT id, key, content_type, tags, created_at, updated_at FROM {SecretsTable} WHERE key = $1";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = key });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadSecretRow(reader);
    }

    public async Task<PostgreSqlVersionRow?> GetVersionAsync(int secretId, int? version, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        string sql;
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        NpgsqlCommand cmd;

        if (version.HasValue)
        {
            sql = $"SELECT id, secret_id, version, value, encrypted, created_at, is_current FROM {VersionsTable} WHERE secret_id = $1 AND version = $2";
            cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = secretId });
            cmd.Parameters.Add(new NpgsqlParameter { Value = version.Value });
        }
        else
        {
            sql = $"SELECT id, secret_id, version, value, encrypted, created_at, is_current FROM {VersionsTable} WHERE secret_id = $1 AND is_current = true";
            cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = secretId });
        }

        await using (cmd)
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return ReadVersionRow(reader);
        }
    }

    public async Task<(PostgreSqlSecretRow Secret, PostgreSqlVersionRow Version)> PutSecretAsync(
        string key, string value, bool encrypted, string? contentType,
        IReadOnlyDictionary<string, string>? tags, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent writes for the same key. Without this lock, two transactions can
        // both read the same MAX(version) and one of them fails on the UNIQUE(secret_id, version)
        // constraint.
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1))", conn, tx))
        {
            lockCommand.Parameters.Add(new NpgsqlParameter { Value = key });
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        // Upsert secret row
        var upsertSql = $"""
            INSERT INTO {SecretsTable} (key, content_type, tags, created_at, updated_at)
            VALUES ($1, $2, $3::jsonb, now(), now())
            ON CONFLICT (key) DO UPDATE
                SET content_type = COALESCE(EXCLUDED.content_type, {SecretsTable}.content_type),
                    tags = COALESCE(EXCLUDED.tags, {SecretsTable}.tags),
                    updated_at = now()
            RETURNING id, key, content_type, tags, created_at, updated_at
            """;

        PostgreSqlSecretRow secretRow;
        await using (var cmd = new NpgsqlCommand(upsertSql, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = key });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)contentType ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter
            {
                Value = tags is { Count: > 0 } ? JsonSerializer.Serialize(tags) : DBNull.Value,
                NpgsqlDbType = NpgsqlDbType.Jsonb
            });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            secretRow = ReadSecretRow(reader);
        }

        // Unmark current version
        var unmarkSql = $"UPDATE {VersionsTable} SET is_current = false WHERE secret_id = $1 AND is_current = true";
        await using (var cmd = new NpgsqlCommand(unmarkSql, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = secretRow.Id });
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert new version
        var insertVersionSql = $"""
            INSERT INTO {VersionsTable} (secret_id, version, value, encrypted, created_at, is_current)
            VALUES ($1,
                    COALESCE((SELECT MAX(version) FROM {VersionsTable} WHERE secret_id = $1), 0) + 1,
                    $2, $3, now(), true)
            RETURNING id, secret_id, version, value, encrypted, created_at, is_current
            """;

        PostgreSqlVersionRow versionRow;
        await using (var cmd = new NpgsqlCommand(insertVersionSql, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = secretRow.Id });
            cmd.Parameters.Add(new NpgsqlParameter { Value = value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = encrypted });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            versionRow = ReadVersionRow(reader);
        }

        await tx.CommitAsync(cancellationToken);

        return (secretRow, versionRow);
    }

    public async Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var sql = $"DELETE FROM {SecretsTable} WHERE key = $1";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = key });

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<List<PostgreSqlVersionRow>> ListVersionsAsync(int secretId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var sql = $"SELECT id, secret_id, version, value, encrypted, created_at, is_current FROM {VersionsTable} WHERE secret_id = $1 ORDER BY version ASC";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = secretId });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<PostgreSqlVersionRow>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadVersionRow(reader));

        return results;
    }

    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var sql = $"SELECT EXISTS (SELECT 1 FROM {SecretsTable} WHERE key = $1)";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = key });

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        await _dataSource.DisposeAsync();
    }

    private static PostgreSqlSecretRow ReadSecretRow(NpgsqlDataReader reader)
    {
        var tagsJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        var tags = tagsJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson)
                as IReadOnlyDictionary<string, string>
            : null;

        return new PostgreSqlSecretRow(
            Id: reader.GetInt32(0),
            Key: reader.GetString(1),
            ContentType: reader.IsDBNull(2) ? null : reader.GetString(2),
            Tags: tags,
            CreatedAt: reader.GetDateTime(4),
            UpdatedAt: reader.GetDateTime(5));
    }

    private static PostgreSqlVersionRow ReadVersionRow(NpgsqlDataReader reader) =>
        new(
            Id: reader.GetInt32(0),
            SecretId: reader.GetInt32(1),
            Version: reader.GetInt32(2),
            Value: reader.GetString(3),
            Encrypted: reader.GetBoolean(4),
            CreatedAt: reader.GetDateTime(5),
            IsCurrent: reader.GetBoolean(6));

    private static void ValidateIdentifier(string value, string paramName)
    {
        if (!SafeIdentifierRegex().IsMatch(value))
            throw new ArgumentException(
                $"{paramName} must contain only letters, digits, or underscores and start with a letter or underscore.",
                paramName);
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex SafeIdentifierRegex();
}
