---
id: ibm-cloud-secrets-manager
title: IBM Cloud Secrets Manager Provider
sidebar_label: IBM Cloud Secrets Manager
sidebar_position: 5
---

# IBM Cloud Secrets Manager Provider

<span className="provider-badge provider-ibm">IBM Cloud Secrets Manager</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The IBM Cloud Secrets Manager provider integrates with IBM Cloud's fully managed secret storage service. It communicates via IBM Cloud's REST API using `HttpClient` -- no SDK dependency required.

**Key Benefits:**
- Global availability across multiple regions
- Enterprise-grade security with IAM-based authentication
- Built on HashiCorp Vault (open source)
- Comprehensive secret lifecycle management
- Integrated with IBM Cloud Event Notifications

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.IBMCloudSecretsManager
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `Region` | `string` | Yes | `""` | IBM Cloud region, e.g. `us-south`, `eu-de` or `jp-tok` |
| `InstanceId` | `string` | Yes | `""` | Instance ID of the Secrets Manager instance |
| `ApiKey` | `string` | Yes | `""` | IBM Cloud API key for authentication |
| `ServiceUrl` | `string?` | No | `null` | Service URL override |

`IBMCloudSecretsManagerOptions.Validate()` requires `Region`, `InstanceId` and `ApiKey`.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithIBMCloudSecretsManager(options =>
    {
        options.Region = "us-south";
        options.InstanceId = "your-instance-id";
        options.ApiKey = "your-api-key";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=ibmcloud-secrets-manager
export SECRETS_IBMCLOUD_SECRETS_MANAGER_REGION=us-south
export SECRETS_IBMCLOUD_SECRETS_MANAGER_INSTANCE_ID=your-instance-id
export SECRETS_IBMCLOUD_SECRETS_MANAGER_API_KEY=your-api-key
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  ibmcloud-secrets-manager:
    region: us-south
    instance.id: your-instance-id
    api.key: your-api-key
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureIBMCloudSecretsManager(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "IBMCloudSecretsManager": {
      "Region": "us-south",
      "InstanceId": "your-instance-id",
      "ApiKey": "your-api-key"
    }
  }
}
```

This reads from the `SecretsManager:IBMCloudSecretsManager` section and validates all options.

## Authentication

IBM Cloud Secrets Manager uses API key-based authentication. You'll need:

1. An **API Key** (generated in IBM Cloud Console)
2. An **Instance ID** (unique identifier for your Secrets Manager instance)
3. A **Region** (where your instance is deployed)

Generate these from the [IBM Cloud Console](https://cloud.ibm.com/) under Secrets Manager service.

:::tip
Store your IBM Cloud credentials in environment variables rather than hardcoding them. Use a different API key per environment for better access control.
:::

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

## Supported Secret Types

The IBM Cloud Secrets Manager provider supports all standard secret types:

- `arbitrary` - Freeform text secrets
- `username_password` - Credential pairs
- `iam_credentials` - IBM IAM service ID credentials
- `imported_cert` - Imported TLS certificates
- `public_cert` - Publicly trusted TLS certificates
- `private_cert` - Privately issued certificates
- `kv` - Key-value pairs

## Regional Endpoints

IBM Cloud Secrets Manager is available in the following regions:

| Region | Endpoint |
|--------|----------|
| Dallas | `{instance_ID}.us-south.secrets-manager.appdomain.cloud` |
| Frankfurt | `{instance_ID}.eu-de.secrets-manager.appdomain.cloud` |
| London | `{instance_ID}.eu-gb.secrets-manager.appdomain.cloud` |
| Madrid | `{instance_ID}.eu-es.secrets-manager.appdomain.cloud` |
| Osaka | `{instance_ID}.jp-osa.secrets-manager.appdomain.cloud` |
| Sao Paulo | `{instance_ID}.br-sao.secrets-manager.appdomain.cloud` |
| Sydney | `{instance_ID}.au-syd.secrets-manager.appdomain.cloud` |
| Tokyo | `{instance_ID}.jp-tok.secrets-manager.appdomain.cloud` |
| Toronto | `{instance_ID}.ca-tor.secrets-manager.appdomain.cloud` |
| Montreal | `{instance_ID}.ca-mon.secrets-manager.appdomain.cloud` |
| Washington DC | `{instance_ID}.us-east.secrets-manager.appdomain.cloud` |

## Rate Limiting

There is **no hard rate limit** enforced by the API. However, as request rate increases, some performance degradation is expected.

**Recommended maximum:** ~20 requests per second.

When rate limits are exceeded, the API responds with HTTP `429 Too Many Requests`. Implement exponential backoff in your client when you encounter 429 responses.

## Error Handling

The IBM Cloud Secrets Manager API uses standard HTTP status codes. All error responses share a consistent JSON structure:

```json
{
  "trace": "f9d9d161-e087-4871-963b-88ea3fe72aca",
  "status_code": 400,
  "errors": [
    {
      "code": "bad_request",
      "message": "required.name: property \"name\" is missing",
      "more_info": "https://cloud.ibm.com/apidocs/secrets-manager"
    }
  ]
}
```

### HTTP Status Code Summary

| Code | Status | Meaning |
|------|--------|---------|
| 200 | OK | Request succeeded |
| 201 | Created | Resource successfully created |
| 300 | Multiple Choices | Request has more than one possible response |
| 400 | Bad Request | Missing or invalid parameter |
| 401 | Unauthorized | IAM token missing or invalid |
| 402 | Payment Required | Trial plan has expired |
| 403 | Forbidden | Insufficient IAM permissions |
| 404 | Not Found | Resource does not exist |
| 409 | Conflict | Resource conflicts with an existing one |
| 410 | Gone | Resource was deleted and no longer exists |
| 429 | Too Many Requests | Rate limit hit |
| 500 | Internal Server Error | Unexpected error on IBM's side |
| 502 | Bad Gateway | Upstream service error |
| 503 | Service Unavailable | Service temporarily unavailable |
