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
| `EncryptionKeyProvider` | `IEncryptionKeyProvider?` | No | `null` | Supplies the encryption key from an external source, for example another secret provider. Fluent API only — it is an object, so it cannot come from appsettings.json, YAML, or an environment variable |
| `LegacyEncryptionKey` | `string?` | No | `null` | Base64-encoded key used only to *decrypt* values written before key ids were recorded. Set this when migrating an existing database to `EncryptionKeyProvider` |

`PostgreSqlOptions.Validate()` requires `ConnectionString`, rejects `EncryptionKey`, `EncryptionKeyFile` and `EncryptionKeyProvider` being set together, and caps `TablePrefix` at 40 characters (PostgreSQL truncates identifiers at 63 bytes, which can collapse the two table names into one). `LegacyEncryptionKey` is only meaningful alongside `EncryptionKeyProvider`. Validation runs on every construction path.

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

### With the Key From Another Provider

The key can be held in a different secret provider entirely — Azure Key Vault, Scaleway, HashiCorp Vault, AWS Secrets Manager, or any of the other providers. It is fetched on first use and cached per key version.

```csharp title="Program.cs"
// The bootstrap provider holds the key. It is built first and owned by the caller.
await using var keyProvider = new SecretProviderBuilder()
    .WithAzureKeyVault(options => options.VaultUri = "https://my-vault.vault.azure.net/")
    .Build();

await using var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
        options.EncryptionKeyProvider = new SecretProviderKeySource(keyProvider, "sm-data-key");
    })
    .Build();
```

Store the key as base64 — `openssl rand -base64 32` — and rotate it by putting a new version of that secret. New writes pick up the new version automatically; existing rows stay readable because each row records the key version it was written with and `GetKeyAsync` fetches that exact version on demand.

The same pattern works with Scaleway:

```csharp
await using var keyProvider = new SecretProviderBuilder()
    .WithScaleway(options =>
    {
        options.Region = "fr-par";
        options.ProjectId = "...";
        options.AccessKey = "...";
        options.SecretKey = "...";
    })
    .Build();

await using var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
        options.EncryptionKeyProvider = new SecretProviderKeySource(keyProvider, "sm/data-key");
    })
    .Build();
```

:::warning
Build the key provider first, and never point a key source at the store it encrypts. A self-referential key source recurses or fails on first use — the fetch happens inside the operation that needs the key, so it cannot bootstrap itself.
:::

`SecretProviderKeySource` does **not** dispose the provider it wraps. The caller owns the bootstrap provider and may use it for other keys.

:::tip
Call `WarmUpAsync()` to resolve the key and ensure the schema at startup rather than on the first read or write. Without it, a misconfigured key source surfaces only when the first secret is accessed.
:::

### Migrating an Existing Database

Every row written before this feature has no key id. When you set `EncryptionKeyProvider`, also set `LegacyEncryptionKey` to the key the existing rows were written with:

```csharp
await using var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
        options.EncryptionKeyProvider = new SecretProviderKeySource(keyProvider, "sm-data-key");
        options.LegacyEncryptionKey = previousBase64Key;
    })
    .Build();
```

Rows without a key id decrypt with `LegacyEncryptionKey`; new writes record the key id from the provider and need it no longer. Re-encrypting the old rows is optional cleanup, not a precondition.

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
