---
id: postgresql
title: PostgreSQL Provider
sidebar_label: PostgreSQL
sidebar_position: 5
---

# PostgreSQL Provider

<span className="provider-badge provider-postgresql">PostgreSQL</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The PostgreSQL provider stores secrets in a PostgreSQL database with optional AES-256-GCM encryption at rest. It uses Npgsql with raw SQL for lightweight, direct database access -- no ORM overhead.

**Key Benefits:**
- Use your existing PostgreSQL infrastructure for secrets
- AES-256-GCM encryption at rest (optional)
- Auto-versioning with per-secret version history
- Optional table auto-creation, with an ownership check on the tables it uses
- Tags stored as JSONB for flexible querying

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.PostgreSql
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `ConnectionString` | `string` | Yes | `""` | PostgreSQL connection string, e.g. `Host=localhost;Database=secrets;Username=app;Password=pass` |
| `Schema` | `string` | No | `"public"` | Database schema for the tables |
| `TablePrefix` | `string` | No | `"sm_"` | Prefix for table names, to avoid collisions |
| `AutoCreateSchema` | `bool` | No | `false` | Create the tables automatically on first use. Requires `CREATE` on the schema |
| `SchemaOwner` | `string?` | No | `null` | Role expected to own the secrets tables. When `null`, the connected role must own them |
| `EncryptionKey` | `string?` | No | `null` | Base64-encoded AES-256 key (32 bytes). When set, values are encrypted at rest with AES-256-GCM |
| `EncryptionKeyFile` | `string?` | No | `null` | Path to a file containing the encryption key. A 32-byte file is used as-is, otherwise it is treated as base64 |

`PostgreSqlOptions.Validate()` requires `ConnectionString`, rejects `EncryptionKey` and `EncryptionKeyFile` being set together, and caps `TablePrefix` at 40 characters (PostgreSQL truncates identifiers at 63 bytes, which can collapse the two table names into one). Validation runs on every construction path.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
        options.Schema = "public";
        options.TablePrefix = "sm_";
        options.EncryptionKey = "base64-encoded-32-byte-key";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=postgresql
export SECRETS_POSTGRESQL_CONNECTION_STRING="Host=localhost;Database=secrets;Username=app;Password=secret"
export SECRETS_POSTGRESQL_SCHEMA=public
export SECRETS_POSTGRESQL_TABLE_PREFIX=sm_
export SECRETS_POSTGRESQL_AUTO_CREATE_SCHEMA=true
export SECRETS_POSTGRESQL_ENCRYPTION_KEY=base64encodedkey
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  postgresql:
    connection:
      string: "Host=localhost;Database=secrets;Username=app;Password=secret"
    schema: public
    table:
      prefix: sm_
    auto:
      create:
        schema: true
    encryption:
      key: base64encodedkey    # or keyFile: /path/to/keyfile
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigurePostgreSql(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "PostgreSql": {
      "ConnectionString": "Host=localhost;Database=secrets;Username=app;Password=pass"
    }
  }
}
```

This reads from the `SecretsManager:PostgreSql` section and validates all options.

### Transport security

Npgsql defaults to `SslMode=Prefer`, which permits an unencrypted connection and does not verify the
server certificate. Add `SSL Mode=VerifyFull` to the connection string in any environment that is not
a trusted local socket:

```text
Host=db.example.com;Database=secrets;Username=app;Password=...;SSL Mode=VerifyFull
```

Without it, the database password, secret keys, tags and any unencrypted values can be read or
modified in transit.

### Least-privilege setup

`AutoCreateSchema` defaults to `false` so the runtime account does not need `CREATE` on the schema.
Create the tables from a migration role and grant the application only DML rights on the two tables:

```sql
CREATE SCHEMA secrets AUTHORIZATION secrets_owner;
REVOKE ALL ON SCHEMA secrets FROM PUBLIC;
GRANT USAGE ON SCHEMA secrets TO secrets_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON secrets.sm_secrets, secrets.sm_secret_versions TO secrets_app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA secrets TO secrets_app;
```

```csharp
options.AutoCreateSchema = false;          // the migration role creates the tables
options.SchemaOwner = "secrets_owner";     // the app connects as secrets_app
```

When `AutoCreateSchema` is `true`, the provider checks `pg_class.relowner` for both tables after the
DDL and throws if the owner is not `SchemaOwner` (or the connected role when it is unset). A role that
can create objects in the target schema could otherwise create the tables first, own them, and read
every secret written afterwards — or add a trigger that copies values elsewhere.

## Encryption

When `EncryptionKey` or `EncryptionKeyFile` is configured, secret values are encrypted before storage using AES-256-GCM.

Encrypted values are stored as JSON in the `value` column:

```json
{"n":"<nonce-base64>","c":"<ciphertext-base64>","t":"<tag-base64>"}
```

Each version row has an `encrypted` boolean column. The provider refuses to mix the two states: a row marked unencrypted while a key is configured, or an encrypted row with no key configured, both throw `SecretProviderException`. This closes the downgrade attack — `UPDATE ... SET encrypted = false, value = 'attacker-value'`, which anyone with database access could otherwise use to forge a secret such as a connection string pointing at their own server. It also means a key cannot simply be added to a database that already holds plaintext rows; those rows must be rewritten.

:::tip
Generate a 32-byte encryption key with:

```bash
openssl rand -base64 32
```
:::

## Database Schema

The provider creates two tables (auto-created when `AutoCreateSchema` is `true`):

### Secrets Table (`sm_secrets`)

| Column | Type | Description |
|--------|------|-------------|
| `id` | `SERIAL` | Primary key |
| `key` | `TEXT UNIQUE` | Secret identifier |
| `content_type` | `TEXT` | MIME type (nullable) |
| `tags` | `JSONB` | Key-value metadata (nullable) |
| `created_at` | `TIMESTAMPTZ` | Creation timestamp |
| `updated_at` | `TIMESTAMPTZ` | Last update timestamp |

### Versions Table (`sm_secret_versions`)

| Column | Type | Description |
|--------|------|-------------|
| `id` | `SERIAL` | Primary key |
| `secret_id` | `INTEGER` | FK to secrets table (cascade delete) |
| `version` | `INTEGER` | Auto-incrementing version per secret |
| `value` | `TEXT` | Secret value (encrypted or plaintext) |
| `encrypted` | `BOOLEAN` | Whether the value is encrypted |
| `created_at` | `TIMESTAMPTZ` | Creation timestamp |
| `is_current` | `BOOLEAN` | Whether this is the latest version |

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

:::info[Connection Pooling]
The provider uses `NpgsqlDataSource` internally, which manages connection pooling automatically. You do not need to configure pooling separately.
:::
