<p align="center">
  <img src="hero.svg" alt="SecretsManager - Unified .NET Secret Management" width="800">
</p>

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT OR Apache-2.0](https://img.shields.io/badge/License-MIT%20OR%20Apache--2.0-blue.svg)](LICENSE-MIT)
[![CI](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml/badge.svg)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)
[![Integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests&label=integration)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

A unified .NET abstraction layer over secret management backends, inspired by the [External Secrets Operator](https://external-secrets.io/) for Kubernetes. Program against a single `ISecretProvider` interface regardless of where your secrets live.

## Features

- **Unified API** — Get, put, delete, version, and check secrets through one interface
- **Multiple backends** — Filesystem, Azure Key Vault, Scaleway, AWS, Google Cloud, Oracle Vault, IBM Cloud, PostgreSQL, OVH, Passbolt, Aliyun KMS, Tencent Cloud, Infisical, HashiCorp Vault, CyberArk Conjur
- **Flexible configuration** — Fluent API, environment variables, or YAML (ESO-style)
- **Secret versioning** — First-class support for secret version history across all providers
- **JSON property extraction** — Retrieve nested values from JSON secrets via dot-path queries
- **Zero unnecessary dependencies** — Each provider is a separate package; only pull in what you use

> **NuGet package prefix:** The `SecretsManager` package ID is already taken on nuget.org by another publisher, so every package in this project is published as `mrhamburg.SecretsManager.*`. The C# namespaces remain `SecretsManager.*`.

## Providers

| Provider | Package | Description |
|----------|---------|-------------|
| Core | `mrhamburg.SecretsManager` | Shared abstractions, builder, and runtime model types |
| FileSystem | `mrhamburg.SecretsManager.FileSystem` | Local encrypted file storage (AES-256-GCM) |
| Azure Key Vault | `mrhamburg.SecretsManager.AzureKeyVault` | Azure Key Vault via official SDK |
| Scaleway | `mrhamburg.SecretsManager.Scaleway` | Scaleway Secret Manager via REST API |
| IBM Cloud Secrets Manager | `mrhamburg.SecretsManager.IBMCloudSecretsManager` | IBM Cloud Secrets Manager via REST API |
| PostgreSQL | `mrhamburg.SecretsManager.PostgreSql` | PostgreSQL-backed encrypted storage with versioning |
| OVH | `mrhamburg.SecretsManager.OVH` | OVH Secret Manager / OKMS via REST API |
| AWS Secrets Manager | `mrhamburg.SecretsManager.AwsSecretsManager` | AWS Secrets Manager via AWS SDK |
| Oracle Vault | `mrhamburg.SecretsManager.OracleVault` | Oracle Cloud Secret Manager via OCI SDK |
| Google Cloud Secret Manager | `mrhamburg.SecretsManager.GoogleSecretManager` | Google Secret Manager via GCP SDK |
| Passbolt | `mrhamburg.SecretsManager.Passbolt` | Passbolt secret management via REST API |
| Aliyun KMS | `mrhamburg.SecretsManager.AliyunKms` | Aliyun KMS via REST API |
| Tencent Cloud | `mrhamburg.SecretsManager.TencentCloud` | Tencent Cloud Secrets Manager via REST API |
| Infisical | `mrhamburg.SecretsManager.Infisical` | Infisical via the official .NET SDK |
| HashiCorp Vault | `mrhamburg.SecretsManager.Vault` | HashiCorp Vault KV v2 engine via REST API |
| CyberArk Conjur | `mrhamburg.SecretsManager.Conjur` | CyberArk Conjur via REST API |

## Integration tests

Integration tests run each provider against a live service in CI. The table shows the
latest result on `main`. A grey `no check runs` pill means the provider's CI secrets
are not configured, so the job is skipped.

| Provider | Integration test config | Latest status on `main` |
| --- | --- | --- |
| FileSystem | [`configs/filesystem.yaml`](tests/SecretsManager.IntegrationTests/configs/filesystem.yaml) | [![FileSystem integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28FileSystem%29&label=filesystem)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Azure Key Vault | [`configs/azure-keyvault.yaml`](tests/SecretsManager.IntegrationTests/configs/azure-keyvault.yaml) | [![Azure Key Vault integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Azure%20Key%20Vault%29&label=azure)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Scaleway | [`configs/scaleway.yaml`](tests/SecretsManager.IntegrationTests/configs/scaleway.yaml) | [![Scaleway integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Scaleway%29&label=scaleway)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| PostgreSQL | [`configs/postgresql.yaml`](tests/SecretsManager.IntegrationTests/configs/postgresql.yaml) | [![PostgreSQL integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28PostgreSQL%29&label=postgresql)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| OVH | [`configs/ovh.yaml`](tests/SecretsManager.IntegrationTests/configs/ovh.yaml) | [![OVH integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28OVH%29&label=ovh)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| AWS Secrets Manager | [`configs/aws-secrets-manager.yaml`](tests/SecretsManager.IntegrationTests/configs/aws-secrets-manager.yaml) | [![AWS integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28AWS%29&label=aws)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Oracle Vault | [`configs/oracle-vault.yaml`](tests/SecretsManager.IntegrationTests/configs/oracle-vault.yaml) | [![Oracle Vault integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Oracle%20Vault%29&label=oracle%20vault)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Google Cloud Secret Manager | [`configs/google-secret-manager.yaml`](tests/SecretsManager.IntegrationTests/configs/google-secret-manager.yaml) | [![Google Secret Manager integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Google%20Secret%20Manager%29&label=google)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Passbolt | [`configs/passbolt.yaml`](tests/SecretsManager.IntegrationTests/configs/passbolt.yaml) | [![Passbolt integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Passbolt%29&label=passbolt)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Aliyun KMS | [`configs/aliyun-kms.yaml`](tests/SecretsManager.IntegrationTests/configs/aliyun-kms.yaml) | [![Aliyun KMS integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Aliyun%20KMS%29&label=aliyun)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| IBM Cloud Secrets Manager | [`configs/ibm-cloud-secrets-manager.yaml`](tests/SecretsManager.IntegrationTests/configs/ibm-cloud-secrets-manager.yaml) | [![IBM Cloud Secrets Manager integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28IBM%20Cloud%20Secrets%20Manager%29&label=ibm)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Tencent Cloud | [`configs/tencent-cloud.yaml`](tests/SecretsManager.IntegrationTests/configs/tencent-cloud.yaml) | [![Tencent Cloud integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Tencent%20Cloud%29&label=tencent)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| HashiCorp Vault | [`configs/vault.yaml`](tests/SecretsManager.IntegrationTests/configs/vault.yaml) | [![HashiCorp Vault integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28HashiCorp%20Vault%29&label=vault)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| Infisical | [`configs/infisical.yaml`](tests/SecretsManager.IntegrationTests/configs/infisical.yaml) | [![Infisical integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Infisical%29&label=infisical)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |
| CyberArk Conjur | [`configs/conjur.yaml`](tests/SecretsManager.IntegrationTests/configs/conjur.yaml) | [![CyberArk Conjur integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28CyberArk%20Conjur%29&label=conjur)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml) |

Passbolt is disabled in CI (`if: false`), so its badge stays grey until the job is enabled.

## Quick Start

```csharp
using SecretsManager;
using SecretsManager.FileSystem;

await using var provider = new SecretProviderBuilder()
    .WithFileSystem(opts =>
    {
        opts.BasePath = "/secrets";
        opts.EncryptionKey = "base64encodedkey==";
    })
    .Build();

// Store a secret
await provider.PutSecretAsync("api-key", "sk-secret-value");

// Retrieve it
var secret = await provider.GetSecretAsync("api-key");
Console.WriteLine(secret.Value);

// Check existence
if (await provider.SecretExistsAsync("api-key"))
    Console.WriteLine("Secret exists!");

// List versions
var versions = await provider.GetSecretVersionsAsync("api-key");

// Delete
await provider.DeleteSecretAsync("api-key");
```

## Layers

Layers are decorators that wrap a provider to add operational capabilities like retries, timeouts, logging, and observability. They are applied in registration order using the builder's fluent API.

### Available Layers

| Layer | Extension Method | Description |
|-------|------------------|-------------|
| `ConcurrentLimiterLayer` | `.WithConcurrentLimiter()` | Limits concurrent operations with an optional acquire timeout |
| `RetryLayer` | `.WithRetry()` | Retries failed operations with exponential backoff and jitter |
| `TimeoutLayer` | `.WithTimeout()` | Enforces a per-operation timeout |
| `LoggingLayer` | `.WithLogging()` | Logs start, success, failure, and retry events. Includes a `Microsoft.Extensions.Logging` adapter |
| `AuditLayer` | `.WithAudit()` / `.WithAuditSink()` | Writes durable per-access audit records without secret values |
| `OtelMetricsLayer` | `.WithOtelMetrics()` | Emits OpenTelemetry-compatible metrics (counters, histograms) |
| `OtelTraceLayer` | `.WithOtelTrace()` | Emits OpenTelemetry-compatible distributed traces |

### Layer Configuration

```csharp
await using var provider = new SecretProviderBuilder()
    .WithFileSystem(opts =>
    {
        opts.BasePath = "/secrets";
        opts.EncryptionKey = "base64encodedkey==";   // omit only for throwaway local development
    })
    .WithConcurrentLimiter(opts => {
        opts.MaxConcurrentRequests = 10;
        opts.AcquireTimeout = TimeSpan.FromSeconds(5);
    })
    .WithRetry(opts => {
        opts.MaxAttempts = 3;
        opts.BaseDelay = TimeSpan.FromMilliseconds(100);
        opts.MaxDelay = TimeSpan.FromSeconds(2);
        opts.JitterFactor = 0.2;
    })
    .WithTimeout(opts => opts.Timeout = TimeSpan.FromSeconds(30))
    .WithLogging(opts => {
        opts.Logger = new LoggerSecretProviderLogger(logger);   // Microsoft.Extensions.Logging
        opts.OperationPrefix = "secrets";
    })
    .WithOtelMetrics(opts => {
        opts.MeterName = "SecretsManager";
        opts.MetricPrefix = "secrets.";
    })
    .WithOtelTrace(opts => {
        opts.ActivitySourceName = "SecretsManager";
        opts.RecordException = true;
    })
    .Build();
```

Layers are processed in registration order (outermost to innermost). The first registered layer wraps the provider, and each subsequent layer wraps the previous one.

### Audit Trail

The audit layer writes one durable record per secret operation: operation, provider, key, version,
outcome, timestamp, actor, trace id, and failure type. Records never contain secret values, and a
failing sink does not change the operation's outcome by default.

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(opts => opts.BasePath = "/secrets")
    .WithAudit(opts =>
    {
        opts.Sinks.Add(new FileSecretAuditSink("/var/log/secrets-audit.jsonl"));
    })
    .Build();
```

`mrhamburg.SecretsManager.PostgreSql` also ships `PostgreSqlSecretAuditSink`, available through
`.WithPostgreSqlAuditSink(...)`. See [Audit Trail](docs/docs/advanced/audit-trail.md) for actor
propagation, scopes, failure modes, and the PostgreSQL table schema.

## Configuration

SecretsManager supports four ways to configure a provider.

### Fluent API

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(opts =>
    {
        opts.BasePath = "/secrets";
        opts.EncryptionKey = "base64encodedkey==";
    })
    .Build();
```

### Dependency Injection (`IOptions<T>`)

Bind options from `appsettings.json` through the DI container:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSecretsManager();
builder.Services.Configure<FileSystemOptions>(
    builder.Configuration.GetSection("SecretsManager:FileSystem"));

// Or bind and build in one step
var provider = new SecretProviderBuilder()
    .ConfigureFileSystem(builder.Configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "FileSystem": {
      "BasePath": "/secrets",
      "EncryptionKey": "base64encodedkey=="
    }
  }
}
```

### Environment Variables

```bash
export SECRETS_PROVIDER=filesystem
export SECRETS_FILESYSTEM_PATH=/secrets
export SECRETS_FILESYSTEM_ENCRYPTION_KEY=base64encodedkey==
```

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem()
    .FromEnvironment()
    .Build();
```

### YAML (External Secrets Operator Style)

```yaml
provider:
  filesystem:
    path: /secrets
    encryption:
      key: base64encodedkey==
```

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem()
    .FromYamlFile("secretstore.yaml")
    .Build();
```


## Supported Provider Docs

See the full provider setup and configuration details in the docs:

- [Getting Started](docs/docs/getting-started/quick-start.md)
- [Provider overview](docs/docs/providers/overview.md)
- [JSON extraction guide](docs/docs/advanced/json-extraction.md)
- [Versioning guide](docs/docs/advanced/versioning.md)
- [Audit trail guide](docs/docs/advanced/audit-trail.md)

## API Reference

All providers implement `ISecretProvider`:

| Method | Description |
|--------|-------------|
| `GetSecretAsync(key, query?, ct)` | Retrieve a secret, optionally by version or JSON property path |
| `PutSecretAsync(key, value, metadata?, ct)` | Create or update a secret |
| `DeleteSecretAsync(key, ct)` | Delete a secret |
| `GetSecretVersionsAsync(key, ct)` | List all versions of a secret |
| `SecretExistsAsync(key, ct)` | Check existence without fetching the value |

`ISecretProvider` extends `IAsyncDisposable` — use `await using` for proper cleanup.

## Building from Source

```bash
dotnet build
dotnet test
```

Run tests with code coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Adding a New Provider

1. Create `src/SecretsManager.<Name>/` with a project reference to the core `SecretsManager` library. Its package ID (`mrhamburg.SecretsManager.<Name>`) is derived automatically from the project name by `src/Directory.Build.props`
2. Implement `ISecretProvider`
3. Implement `ISecretProviderFactory` to map flat configuration dictionaries to your options
4. Add a `SecretProviderBuilderExtensions` class with `.With<Name>()` extension method
5. Add a test project under `tests/SecretsManager.<Name>.Tests/`
6. Register both projects in `SecretsManager.sln`
7. Add an `integration-test` CI job named `Integration tests (<Name>)`, include it in the
   `integration-status` `needs` list, and add its badge row to `README.md` and
   `docs/docs/providers/overview.md`

## License

This project is dual-licensed under your choice of:

- [MIT License](LICENSE-MIT)
- [Apache License 2.0](LICENSE-APACHE)
