---
id: filesystem
title: FileSystem Provider
sidebar_label: FileSystem
sidebar_position: 2
---

# FileSystem Provider

<span className="provider-badge provider-fs">FileSystem</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The FileSystem provider stores secrets locally with optional AES-256-GCM encryption. It's ideal for local development, edge deployments, and air-gapped environments where cloud connectivity isn't available.

**Key Benefits:**
- Zero external dependencies -- works completely offline
- AES-256-GCM encryption at rest
- Built-in versioning with atomic writes
- Per-key file locking for concurrent access safety

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.FileSystem
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `BasePath` | `string` | Yes | `{LocalApplicationData}/SecretsManager` | Root directory where secrets are stored. Each secret gets a subdirectory |
| `EncryptionKey` | `string?` | No | `null` | Base64-encoded AES-256 key (32 bytes). When set, secrets are encrypted at rest |
| `EncryptionKeyFile` | `string?` | No | `null` | Path to a file containing the raw or base64-encoded AES-256 key |
| `EncryptionKeyProvider` | `IEncryptionKeyProvider?` | No | `null` | Supplies the encryption key from an external source, for example another secret provider. Fluent API only — it is an object, so it cannot come from appsettings.json, YAML, or an environment variable |
| `LegacyEncryptionKey` | `string?` | No | `null` | Base64-encoded key used only to *decrypt* values written before key ids were recorded. Set this when migrating an existing store to `EncryptionKeyProvider` |

`FileSystemOptions.Validate()` requires `BasePath`. `EncryptionKey`, `EncryptionKeyFile` and `EncryptionKeyProvider` are **mutually exclusive** — setting more than one is a validation error. `LegacyEncryptionKey` is only meaningful alongside `EncryptionKeyProvider`. With no key source set at all, secrets are stored in plaintext (development only).

Secret keys are validated before any filesystem access: an empty key, or a key of `.` or `..`, is rejected with `ArgumentException`. Version identifiers must match `v<number>` (for example `v1`); anything else — including `current`, a path separator, or an absolute path — is rejected the same way. Both checks exist because a key or version previously reached the filesystem unchecked, letting `../other-secret/v1` read a different secret and `.` resolve to the store root.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/var/secrets";
        options.EncryptionKey = "base64-encoded-256-bit-key";
    })
    .Build();
```

### With Key File

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/var/secrets";
        options.EncryptionKeyFile = "/etc/secrets/master.key";
    })
    .Build();
```

### With the Key From Another Provider

The key can be held in a different secret provider entirely — Scaleway, Azure Key Vault, HashiCorp Vault, AWS Secrets Manager, or any of the other providers. The key is fetched on first use and cached per key version, so the steady-state cost is one read per version, not one per operation.

```csharp title="Program.cs"
// The bootstrap provider holds the key. It is built first and owned by the caller.
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
    .WithFileSystem(options =>
    {
        options.BasePath = "/var/secrets";
        options.EncryptionKeyProvider = new SecretProviderKeySource(keyProvider, "sm/data-key");
    })
    .Build();
```

Store the key as base64 — `openssl rand -base64 32` — in a secret named `sm/data-key`. Rotating it is a single `PutSecretAsync` at the source, but note that **the current key is cached per process**: new writes pick up the new version when the process restarts, or immediately if you call `InvalidateKey()`. Existing values stay readable either way, because each envelope records the key version it was written with and `GetKeyAsync` fetches that exact version on demand. No keyring list and no separate rotation step.

`WarmUpAsync()` and `InvalidateKey()` are declared on the concrete provider types, not on `ISecretProvider`, so they are unavailable once a layer wraps the provider.

The same pattern works with Azure Key Vault:

```csharp
await using var keyProvider = new SecretProviderBuilder()
    .WithAzureKeyVault(options => options.VaultUri = "https://my-vault.vault.azure.net/")
    .Build();

await using var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/var/secrets";
        options.EncryptionKeyProvider = new SecretProviderKeySource(keyProvider, "sm-data-key");
    })
    .Build();
```

:::warning
Build the key provider first, and never point a key source at the store it encrypts. A self-referential key source recurses or fails on first use — the fetch happens inside the operation that needs the key, so it cannot bootstrap itself.
:::

The key id is **derived from the version the source reports** (`{key}@{version}`), so the source must report a **distinct, immutable version for every key it serves**. A source whose version does not change on rotation — Passbolt always reports `1` — cannot be used as a key source: `SecretProviderKeySource` throws rather than silently reusing a key id, which would make values written under the previous key unreadable.

`SecretProviderKeySource` does **not** dispose the provider it wraps. The caller owns the bootstrap provider and may use it for other keys.

:::tip
Call `WarmUpAsync()` to resolve the key (and, for PostgreSQL, ensure the schema) at startup rather than on the first read or write. Without it, a misconfigured key source surfaces only when the first secret is accessed.
:::

### Migrating an Existing Store

Every envelope written before this feature has no key id. When you set `EncryptionKeyProvider`, also set `LegacyEncryptionKey` to the key the existing values were written with:

```csharp
await using var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/var/secrets";
        options.EncryptionKeyProvider = new SecretProviderKeySource(keyProvider, "sm/data-key");
        options.LegacyEncryptionKey = previousBase64Key;
    })
    .Build();
```

Values without a key id decrypt with `LegacyEncryptionKey`; new writes record the key id from the provider and need it no longer. Re-encrypting the old values is optional cleanup, not a precondition.

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  filesystem:
    path: /var/secrets
    encryption:
      key: base64encodedkey
      # or: keyFile: /etc/secrets/master.key
```

### Unencrypted Mode

For development only, you can disable encryption:

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/tmp/dev-secrets";
        // No encryption key = plaintext storage
    })
    .Build();
```

:::warning
Never use unencrypted mode in production. Always provide an encryption key for any environment that handles real secrets.

Unencrypted mode stores values in cleartext JSON on disk with **no integrity protection**: anyone who can write to the store can change a secret's value undetectably.

Once an encryption key is configured, the provider **refuses to read** any envelope stored unencrypted, and refuses to read an encrypted envelope when no key is configured. Both cases throw `SecretProviderException` rather than returning a value. Migrating an existing unencrypted store therefore means rewriting every secret, not just adding a key.
:::

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureFileSystem(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "FileSystem": {
      "BasePath": "/var/secrets",
      "EncryptionKey": "base64-encoded-256-bit-key"
    }
  }
}
```

This reads from the `SecretsManager:FileSystem` section and validates all options.

## Storage Format

Secrets are stored under `basePath/{key}/` with per-version JSON files:

```
basePath/
  my-secret/
    current.json    -> { "version": "v3" }
    v1.json         -> VersionedSecretEnvelope (encrypted)
    v2.json
    v3.json
```

Each version file contains the encrypted payload, metadata, and timestamp. The `current.json` pointer enables atomic version switching.

## Versioning

The FileSystem provider automatically creates a new version on each `PutSecretAsync` call:

```csharp
// Each put creates a new version
await provider.PutSecretAsync("db-password", "first-value");
await provider.PutSecretAsync("db-password", "second-value");

// List all versions
var versions = await provider.GetSecretVersionsAsync("db-password");
// Returns: v1 (first-value), v2 (second-value)
```
