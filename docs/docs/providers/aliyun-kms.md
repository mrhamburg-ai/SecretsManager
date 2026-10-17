---
id: aliyun-kms
title: Aliyun KMS Provider
sidebar_label: Aliyun KMS
sidebar_position: 6
---

# Aliyun KMS Provider

<span className="provider-badge provider-aliyun">Aliyun KMS</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Aliyun KMS provider integrates with [Alibaba Cloud Key Management Service](https://www.alibabacloud.com/product/key-management-service), a fully managed secret management service. It communicates via the KMS RPC API using `HttpClient` with HMAC-SHA1 signature authentication.

**Key Benefits:**
- Fully managed by Alibaba Cloud
- Server-side encryption with KMS-managed keys
- Automatic version rotation for generic secrets
- Native integration with other Alibaba Cloud services
- Regional availability across global Alibaba Cloud data centers

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.AliyunKms
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `Region` | `string` | Yes | `""` | Alibaba Cloud region ID, e.g. `cn-hangzhou` or `eu-central-1` |
| `AccessKeyId` | `string` | Yes | `""` | Alibaba Cloud AccessKey ID (`LTAI5t...`) |
| `AccessKeySecret` | `string` | Yes | `""` | Alibaba Cloud AccessKey secret |
| `Endpoint` | `string?` | No | `null` | Custom API endpoint override; omit to use the regional default |

`AliyunKmsOptions.Validate()` requires `Region`, `AccessKeyId` and `AccessKeySecret`.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAliyunKms(options =>
    {
        options.Region = "cn-hangzhou";
        options.AccessKeyId = "LTAI5t...";
        options.AccessKeySecret = "abc123...";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=aliyunKms
export SECRETS_ALIYUNKMS_REGION=cn-hangzhou
export SECRETS_ALIYUNKMS_ACCESSKEY=LTAI5t...
export SECRETS_ALIYUNKMS_SECRETKEY=abc123...
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  aliyunKms:
    region: cn-hangzhou
    access:
      key: LTAI5t...
    secret:
      key: abc123...
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureAliyunKms(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "AliyunKms": {
      "Region": "cn-hangzhou",
      "AccessKeyId": "LTAI5t...",
      "AccessKeySecret": "abc123..."
    }
  }
}
```

This reads from the `SecretsManager:AliyunKms` section and validates all options.

## Authentication

Aliyun KMS uses AccessKey-based authentication with HMAC-SHA1 request signing. You'll need:

1. **Region** — the Alibaba Cloud region ID (e.g. `cn-hangzhou`, `ap-southeast-1`)
2. **AccessKey ID** — your Alibaba Cloud AccessKey ID
3. **AccessKey Secret** — your Alibaba Cloud AccessKey Secret

### Creating AccessKeys

1. Log in to the [Alibaba Cloud Console](https://console.aliyun.com/)
2. Navigate to **RAM** (Resource Access Management)
3. Create or select a RAM user
4. Generate an AccessKey pair under the user's security settings
5. Grant the RAM user the `AliyunKMSFullAccess` policy

:::warning
Never commit your AccessKey secret to version control. Use environment variables or a secrets manager for production deployments.
:::

## How It Works

The provider maps Aliyun KMS concepts to the `ISecretProvider` interface:

| ISecretProvider | Aliyun KMS |
|----------------|------------|
| Secret key | Secret name |
| Secret value | SecretData (server-side encrypted) |
| Version | VersionId (UUID-based, auto-generated) |
| Metadata | Not directly mapped |

### Secret Types

The provider creates **Generic** secrets by default. Aliyun KMS supports several secret types (Generic, Rds, Redis, RAMCredentials, ECS), but only Generic secrets support manual version management via `PutSecretValue`.

### Versioning

Aliyun KMS supports up to 10 versions per generic secret. When the limit is reached, the oldest version is automatically deleted in a rolling manner. Each version is labeled with `ACSCurrent` (latest) or `ACSPrevious` (previous).

### Encryption

All secret values are encrypted server-side by KMS using a symmetric key. The encryption is transparent — you work with plaintext values, and KMS handles encryption/decryption automatically.

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
