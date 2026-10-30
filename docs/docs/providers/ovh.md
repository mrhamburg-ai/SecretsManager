---
id: ovh
title: OVH Provider
sidebar_label: OVH
sidebar_position: 6
---

# OVH Provider

<span className="provider-badge provider-ovh">OVH</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

[![OVH integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28OVH%29&label=ovh)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

The OVH integration test runs against a live OVH OKMS on `main`. A grey `no check runs`
pill means the CI secrets are not configured, so the job is skipped.

## Overview

The OVH provider integrates with OVH Keys Management Service (OKMS), a secure secret storage solution hosted in OVH's European data centers. It communicates via OVH's REST API using `HttpClient` -- no SDK dependency required.

**Key Benefits:**
- GDPR-compliant European data residency
- Secure OAuth-based authentication
- Fully managed encryption
- Integration with OVH's cloud infrastructure

## Installation

```bash title="Terminal"
dotnet add package mrhamburg.SecretsManager
dotnet add package mrhamburg.SecretsManager.OVH
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `Endpoint` | `string` | Yes | `""` | OVH endpoint region: `ovh-eu`, `ovh-us` or `ovh-ca` |
| `ApplicationKey` | `string` | Yes | `""` | OVH application key |
| `ApplicationSecret` | `string` | Yes | `""` | OVH application secret |
| `ConsumerKey` | `string` | Yes | `""` | OVH consumer key |
| `OkmsId` | `string` | No | `""` | OVH OKMS ID |

`OVHOptions.Validate()` requires `Endpoint`, `ApplicationKey`, `ApplicationSecret` and `ConsumerKey`.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithOVH(options =>
    {
        options.Endpoint = "ovh-eu";
        options.ApplicationKey = "your-application-key";
        options.ApplicationSecret = "your-application-secret";
        options.ConsumerKey = "your-consumer-key";
        options.OkmsId = "your-okms-id";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=ovh
export SECRETS_OVH_ENDPOINT=ovh-eu
export SECRETS_OVH_APPLICATIONKEY=your-application-key
export SECRETS_OVH_APPLICATIONSECRET=your-application-secret
export SECRETS_OVH_CONSUMERKEY=your-consumer-key
export SECRETS_OVH_OKMSID=your-okms-id
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  ovh:
    endpoint: ovh-eu
    application:
      key: your-application-key
    application:
      secret: your-application-secret
    consumer:
      key: your-consumer-key
    okms:
      id: your-okms-id
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureOVH(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "OVH": {
      "Endpoint": "ovh-eu",
      "ApplicationKey": "your-application-key",
      "ApplicationSecret": "your-application-secret",
      "ConsumerKey": "your-consumer-key"
    }
  }
}
```

This reads from the `SecretsManager:OVH` section and validates all options.

## Authentication

OVH uses OAuth-based authentication. You'll need:

1. An **Application Key** (UUID format)
2. An **Application Secret** (UUID format)
3. A **Consumer Key** (UUID format)
4. An **OKMS ID** (UUID format)

Generate these from the [OVH Console](https://console.cloud.ovh.com/) under Keys Management Service.

:::tip
Store your OVH credentials in environment variables rather than hardcoding them. Use a different API key per environment for better access control.
:::

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```
