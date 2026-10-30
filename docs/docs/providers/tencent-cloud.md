---
id: tencent-cloud
title: Tencent Cloud Secrets Manager Provider
sidebar_label: Tencent Cloud
sidebar_position: 7
---

# Tencent Cloud Secrets Manager Provider

<span className="provider-badge provider-tencent">Tencent Cloud</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

[![Tencent Cloud integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Tencent%20Cloud%29&label=tencent)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

The Tencent Cloud integration test runs against a live Tencent Cloud KMS on `main`. A grey
`no check runs` pill means the CI secrets are not configured, so the job is skipped.

## Overview

The Tencent Cloud Secrets Manager provider integrates with [Tencent Cloud Key Management Service](https://cloud.tencent.com/product/kms), a fully managed secret management service. It communicates via the Tencent Cloud API using `HttpClient` with HMAC-SHA256 signature authentication.

**Key Benefits:**
- Fully managed by Tencent Cloud
- Server-side encryption with KMS-managed keys
- Automatic version rotation for secrets
- Integrated with other Tencent Cloud services
- Regional availability across global Tencent Cloud data centers

## Installation

```bash title="Terminal"
dotnet add package mrhamburg.SecretsManager
dotnet add package mrhamburg.SecretsManager.TencentCloud
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `Region` | `string` | Yes | `""` | Tencent Cloud region, e.g. `ap-shanghai` |
| `SecretId` | `string` | Yes | `""` | Tencent Cloud secret ID |
| `SecretKey` | `string` | Yes | `""` | Tencent Cloud secret key |
| `Endpoint` | `string?` | No | `null` | Custom API endpoint override |

`TencentCloudOptions.Validate()` requires `Region`, `SecretId` and `SecretKey`.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithTencentCloud(options =>
    {
        options.Region = "ap-beijing";
        options.SecretId = "AKID123...";
        options.SecretKey = "abc123...";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=tencentCloud
export SECRETS_TENCENTCLOUD_REGION=ap-beijing
export SECRETS_TENCENTCLOUD_SECRETID=AKID123...
export SECRETS_TENCENTCLOUD_SECRETKEY=abc123...
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  tencentCloud:
    region: ap-beijing
    secret:
      id: AKID123...
      key: abc123...
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureTencentCloud(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "TencentCloud": {
      "Region": "ap-shanghai",
      "SecretId": "your-secret-id",
      "SecretKey": "your-secret-key"
    }
  }
}
```

This reads from the `SecretsManager:TencentCloud` section and validates all options.

## Authentication

Tencent Cloud Secrets Manager uses HMAC-SHA256 signature authentication. You'll need:

1. **Region** — the Tencent Cloud region ID (e.g. `ap-beijing`, `ap-shanghai`)
2. **SecretId** — your Tencent Cloud SecretId
3. **SecretKey** — your Tencent Cloud SecretKey

### Creating Credentials

1. Log in to the [Tencent Cloud Console](https://console.cloud.tencent.com/)
2. Navigate to **API Key Management** (API Gateway or Tencent Cloud Console settings)
3. Create or select an API key
4. Ensure the key has permissions for Secrets Manager operations

:::warning
Never commit your SecretKey to version control. Use environment variables or a secrets manager for production deployments.
:::

## How It Works

The provider maps Tencent Cloud Secrets Manager concepts to the `ISecretProvider` interface:

| ISecretProvider | Tencent Cloud |
|----------------|---------------|
| Secret key | Secret name |
| Secret value | SecretValue (server-side encrypted) |
| Version | VersionId (UUID-based, auto-generated) |
| Metadata | Not directly mapped |

### Secret Versions

Tencent Cloud Secrets Manager supports up to 10 versions per secret. When the limit is reached, the oldest version is automatically deleted in a rolling manner. Each version is labeled with `Latest` (current) or `Previous` (older).

### Encryption

All secret values are encrypted server-side by KMS using a symmetric key. The encryption is transparent — you work with plaintext values, and Tencent Cloud handles encryption/decryption automatically.

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");

// Get a specific version
var oldVersion = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "v1" });

// Create or update (automatically creates new version)
await provider.PutSecretAsync("my-secret", "new-value");

// List all versions
var versions = await provider.GetSecretVersionsAsync("my-secret");

// Delete (immediate, no recovery window)
await provider.DeleteSecretAsync("old-secret");
```
