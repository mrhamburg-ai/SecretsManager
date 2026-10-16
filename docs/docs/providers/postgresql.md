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
- Auto-creates tables on first use
- Tags stored as JSONB for flexible querying

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.PostgreSql
```

## Configuration

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

## Options Reference

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionString` | *(required)* | PostgreSQL connection string |
| `Schema` | `public` | Database schema for the tables |
| `TablePrefix` | `sm_` | Prefix for table names to avoid collisions |
| `AutoCreateSchema` | `true` | Auto-create tables on first use |
| `EncryptionKey` | `null` | Base64-encoded AES-256 key (32 bytes) |
| `EncryptionKeyFile` | `null` | Path to file containing the encryption key |

## Encryption

When `EncryptionKey` or `EncryptionKeyFile` is configured, secret values are encrypted before storage using AES-256-GCM.

Encrypted values are stored as JSON in the `value` column:

```json
{"n":"<nonce-base64>","c":"<ciphertext-base64>","t":"<tag-base64>"}
```

Each version row has an `encrypted` boolean column, allowing mixed encrypted and plaintext versions. This supports key rotation scenarios where new versions are encrypted while old versions remain readable.

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

### Configuration via IOptions<T>

You can also bind configuration from `IConfiguration`:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigurePostgreSql(configuration)
    .Build();
```

This reads from the `SecretsManager:PostgreSql` section and validates all options.
