---
id: scaleway
title: Scaleway Provider
sidebar_label: Scaleway
sidebar_position: 4
---

# Scaleway Provider

<span className="provider-badge provider-scaleway">Scaleway</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Scaleway provider integrates with Scaleway Secret Manager, a European cloud-native secret storage service. It communicates via Scaleway's REST API using `HttpClient` -- no SDK dependency required.

**Key Benefits:**
- GDPR-compliant European data residency (fr-par, nl-ams)
- Competitive pricing with generous free tier
- Simple API key authentication
- Fully managed encryption

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.Scaleway
```

## Configuration

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithScaleway(options =>
    {
        options.Region = "fr-par";
        options.ProjectId = "your-project-uuid";
        options.AccessKey = "your-access-key";
        options.SecretKey = "your-secret-key";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=scaleway
export SECRETS_SCALEWAY_REGION=fr-par
export SECRETS_SCALEWAY_PROJECTID=your-project-uuid
export SECRETS_SCALEWAY_ACCESSKEY=your-access-key
export SECRETS_SCALEWAY_SECRETKEY=your-secret-key
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  scaleway:
    region: fr-par
    project:
      id: your-project-uuid
    access:
      key: your-access-key
    secret:
      key: your-secret-key
```

## Authentication

Scaleway uses API key-based authentication. You'll need:

1. An **Access Key** (SCWXXXXXXXXXXXXXXXXX)
2. A **Secret Key** (UUID format)
3. A **Project ID** (UUID format)

Generate these from the [Scaleway Console](https://console.scaleway.com/) under IAM > API Keys.

:::tip
Store your Scaleway credentials in environment variables rather than hardcoding them. Use a different API key per environment for better access control.
:::

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

### Configuration via IOptions<T>

You can also bind configuration from `IConfiguration`:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureScaleway(configuration)
    .Build();
```

This reads from the `SecretsManager:Scaleway` section and validates all options.
