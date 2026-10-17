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

`FileSystemOptions.Validate()` requires `BasePath`. `EncryptionKey` and `EncryptionKeyFile` are **mutually exclusive** — setting both is a validation error. With neither set, secrets are stored in plaintext (development only).

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
