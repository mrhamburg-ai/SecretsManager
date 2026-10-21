---
id: audit-trail
title: Audit Trail
sidebar_label: Audit Trail
sidebar_position: 3
---

# Audit Trail

The audit layer writes a durable record for every secret operation: who accessed which key, when it
happened, and whether it succeeded. It is separate from logging and metrics:

| Concern | Purpose | Retention |
|---|---|---|
| Logging | Operator diagnostics | Short, sampled |
| Metrics | Aggregates and alerts | Time series |
| Audit trail | Per-access compliance record | Long, durable |

Audit records never contain a secret value. Failed operations record the exception *type*, not the
exception message, because backend messages can echo the submitted payload.

## Usage

```csharp
await using var provider = new SecretProviderBuilder()
    .WithVault(options =>
    {
        options.Url = "https://vault:8200";
        options.Token = token;
    })
    .WithAudit(options =>
    {
        options.Sinks.Add(new FileSecretAuditSink("/var/log/secrets-audit.jsonl"));
        options.Scope = SecretAuditScope.All;
        options.OnSinkFailure = AuditFailureMode.Ignore;
        options.OnSinkError = ex => logger.LogError(ex, "audit sink failed");
    })
    .Build();
```

`WithAuditSink` is the shorthand when only a sink is needed:

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options => options.BasePath = "/etc/secrets")
    .WithAuditSink(new FileSecretAuditSink("/var/log/secrets-audit.jsonl"))
    .Build();
```

The builder fills in the registered provider name automatically. Records contain:

| Property | Description |
|---|---|
| `Operation` | `GetSecretAsync`, `PutSecretAsync`, `DeleteSecretAsync`, `GetSecretVersionsAsync`, `SecretExistsAsync`, or `DisposeAsync` |
| `Provider` | Registered provider name, for example `vault` or `postgresql` |
| `Key` | Target secret key, when the operation targets one |
| `Version` | Resolved version, when the operation returns one |
| `Outcome` | `Success`, `Failure`, or `Denied` |
| `Timestamp` | Operation start time, captured by the library |
| `Duration` | How long the operation took |
| `ActorId` / `ActorDisplayName` | Identity from the ambient actor accessor |
| `CorrelationId` | From `Activity` baggage, when the host set `correlation.id` or `correlation-id` |
| `TraceId` | Current `Activity` trace id |
| `FailureType` | Exception type name when the operation failed |

## Scope

`Scope` selects which operations are audited. The default is `All`.

```csharp
.WithAudit(options =>
{
    options.Scope = SecretAuditScope.Reads | SecretAuditScope.Deletes;
})
```

| Value | Operations |
|---|---|
| `Reads` | `GetSecretAsync` |
| `Writes` | `PutSecretAsync` |
| `Deletes` | `DeleteSecretAsync` |
| `Enumeration` | `GetSecretVersionsAsync`, `SecretExistsAsync` |
| `All` | Everything above, plus `DisposeAsync` |

`SecretExistsAsync` and `GetSecretVersionsAsync` are treated as enumeration because they reveal
secret names even when they do not reveal values.

## Actor propagation

The library cannot infer the caller. Push an actor per request or message scope:

```csharp
public sealed class SecretActorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISecretActorAccessor accessor)
    {
        using var _ = accessor.Push(new SecretActor(
            Id: context.User.FindFirst("sub")?.Value ?? "anonymous",
            DisplayName: context.User.Identity?.Name));

        await next(context);
    }
}
```

The default accessor is `AmbientSecretActorAccessor.Instance`, which flows the actor through
`AsyncLocal<T>`, so it follows async continuations. Set `AuditLayerOptions.ActorAccessor` to use a
different implementation.

## Failure isolation

By default, a broken sink never changes the outcome of a secret operation:

- Every sink call is wrapped.
- Sink errors go to `OnSinkError`; they do not propagate.
- Audit writes use `CancellationToken.None`, so a record is still written when the caller cancels.
- On a failed operation the original exception is always rethrown, even with
  `OnSinkFailure = AuditFailureMode.Throw`.

Set `OnSinkFailure = AuditFailureMode.Throw` only when the audit trail is a hard requirement, and
document the resulting outage risk. It is not the default.

## File sink

`FileSecretAuditSink` writes one JSON object per line (JSON Lines), append-only.

```csharp
var sink = new FileSecretAuditSink("/var/log/secrets-audit.jsonl", new FileSecretAuditSinkOptions
{
    FlushEachWrite = true,
    MaxFileSizeBytes = 100 * 1024 * 1024
});
```

| Option | Type | Required | Default | Description |
|---|---|---|---|---|
| `FlushEachWrite` | `bool` | No | `true` | Force each record to durable storage. An unflushed audit record is not an audit record |
| `MaxFileSizeBytes` | `long?` | No | `null` | Rotate to `<path>.<UTC timestamp>` when the file reaches this size. Rotated files are never overwritten or deleted |

The parent directory is created when it does not exist.

## PostgreSQL sink

The `SecretsManager.PostgreSql` package ships `PostgreSqlSecretAuditSink`:

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
    })
    .WithPostgreSqlAuditSink(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
        options.Schema = "public";
        options.TableName = "sm_secret_audit_log";
        options.AutoCreateSchema = true;
    })
    .Build();
```

| Option | Type | Required | Default | Description |
|---|---|---|---|---|
| `ConnectionString` | `string` | Yes | `""` | PostgreSQL connection string |
| `Schema` | `string` | No | `"public"` | Schema for the audit table |
| `TableName` | `string` | No | `"sm_secret_audit_log"` | Audit table name |
| `AutoCreateSchema` | `bool` | No | `false` | Create the table and timestamp index on first write. Requires `CREATE` on the schema |

`Schema` and `TableName` must be letters, digits, or underscores and start with a letter or
underscore. `TableName` is capped at 63 characters.

With `AutoCreateSchema = false`, create the table from a migration role:

```sql
CREATE TABLE public.sm_secret_audit_log (
    id                   BIGSERIAL PRIMARY KEY,
    "timestamp"          TIMESTAMPTZ NOT NULL,
    "operation"          TEXT NOT NULL,
    "provider"           TEXT NOT NULL,
    "key"                TEXT,
    "version"            TEXT,
    "outcome"            TEXT NOT NULL,
    "duration_ms"        DOUBLE PRECISION NOT NULL,
    "actor_id"           TEXT,
    "actor_display_name" TEXT,
    "correlation_id"     TEXT,
    "trace_id"           TEXT,
    "failure_type"       TEXT
);

CREATE INDEX sm_secret_audit_log_timestamp_idx
    ON public.sm_secret_audit_log ("timestamp");

GRANT SELECT, INSERT ON public.sm_secret_audit_log TO secrets_app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO secrets_app;
```

The sink stores `Outcome` as text (`Success`, `Failure`, `Denied`) and `Duration` as milliseconds.
It uses the same Npgsql connection string conventions as the PostgreSQL provider; add
`SSL Mode=VerifyFull` when the database is not a trusted local socket.

## Configuration

Sinks are objects, so they cannot come from `appsettings.json`. The policy can:

```json
{
  "SecretsManager": {
    "Audit": {
      "Enabled": true,
      "Scope": "All",
      "OnSinkFailure": "Ignore"
    }
  }
}
```

```csharp
var provider = new SecretProviderBuilder()
    .ConfigurePostgreSql(configuration)
    .ConfigureAudit(configuration)
    .WithPostgreSqlAuditSink(options =>
    {
        options.ConnectionString = configuration.GetConnectionString("Audit")!;
        options.AutoCreateSchema = true;
    })
    .Build();
```

`ConfigureAudit` adds the layer with the policy from configuration. A sink must still be registered
with `WithAuditSink` or `WithPostgreSqlAuditSink`. Build fails when the layer is enabled and has no
sinks.

## Custom sinks

Implement `ISecretAuditSink`. Implementations must be thread-safe:

```csharp
public sealed class MyAuditSink : ISecretAuditSink
{
    public ValueTask WriteAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        // Forward to your SIEM or database.
        return ValueTask.CompletedTask;
    }
}
```

The interface is deliberately small so database and SIEM connectors stay consumer-provided unless a
package ships one, as `SecretsManager.PostgreSql` does.
