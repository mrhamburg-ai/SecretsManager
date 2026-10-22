<p align="center">
  <img src="hero.svg" alt="SecretsManager - Unified .NET Secret Management" width="800">
</p>

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT OR Apache-2.0](https://img.shields.io/badge/License-MIT%20OR%20Apache--2.0-blue.svg)](LICENSE-MIT)
[![CI](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml/badge.svg)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

A unified .NET abstraction layer over secret management backends, inspired by the [External Secrets Operator](https://external-secrets.io/) for Kubernetes. Program against a single `ISecretProvider` interface regardless of where your secrets live.

## Features

- **Unified API** — Get, put, delete, version, and check secrets through one interface
- **Multiple backends** — Filesystem, Azure Key Vault, Scaleway, AWS, Google Cloud, Oracle Vault, IBM Cloud, PostgreSQL, OVH, Passbolt, Aliyun KMS, Tencent Cloud, HashiCorp Vault, CyberArk Conjur
- **Flexible configuration** — Fluent API, environment variables, or YAML (ESO-style)
- **Secret versioning** — First-class support for secret version history across all providers
- **JSON property extraction** — Retrieve nested values from JSON secrets via dot-path queries
- **Zero unnecessary dependencies** — Each provider is a separate package; only pull in what you use

## Providers

| Provider | Package | Description |
|----------|---------|-------------|
| Core | `SecretsManager.Core` | Shared abstractions, builder, and runtime model types |
| FileSystem | `SecretsManager.FileSystem` | Local encrypted file storage (AES-256-GCM) |
| Azure Key Vault | `SecretsManager.AzureKeyVault` | Azure Key Vault via official SDK |
| Scaleway | `SecretsManager.Scaleway` | Scaleway Secret Manager via REST API |
| IBM Cloud Secrets Manager | `SecretsManager.IBMCloudSecretsManager` | IBM Cloud Secrets Manager via REST API |
| PostgreSQL | `SecretsManager.PostgreSql` | PostgreSQL-backed encrypted storage with versioning |
| OVH | `SecretsManager.OVH` | OVH Secret Manager / OKMS via REST API |
| AWS Secrets Manager | `SecretsManager.AwsSecretsManager` | AWS Secrets Manager via AWS SDK |
| Oracle Vault | `SecretsManager.OracleVault` | Oracle Cloud Secret Manager via OCI SDK |
| Google Cloud Secret Manager | `SecretsManager.GoogleSecretManager` | Google Secret Manager via GCP SDK |
| Passbolt | `SecretsManager.Passbolt` | Passbolt secret management via REST API |
| Aliyun KMS | `SecretsManager.AliyunKms` | Aliyun KMS via REST API |
| Tencent Cloud | `SecretsManager.TencentCloud` | Tencent Cloud Secrets Manager via REST API |
| HashiCorp Vault | `SecretsManager.Vault` | HashiCorp Vault KV v2 engine via REST API |
| CyberArk Conjur | `SecretsManager.Conjur` | CyberArk Conjur via REST API |

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
| `LoggingLayer` | `.WithLogging()` | Logs operation start, success, and failure events |
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
        opts.Logger = new MySecretProviderLogger();
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

`SecretsManager.PostgreSql` also ships `PostgreSqlSecretAuditSink`, available through
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

1. Create `src/SecretsManager.<Name>/` with a project reference to the core `SecretsManager` library
2. Implement `ISecretProvider`
3. Implement `ISecretProviderFactory` to map flat configuration dictionaries to your options
4. Add a `SecretProviderBuilderExtensions` class with `.With<Name>()` extension method
5. Add a test project under `tests/SecretsManager.<Name>.Tests/`
6. Register both projects in `SecretsManager.sln`

## License

This project is dual-licensed under your choice of:

- [MIT License](LICENSE-MIT)
- [Apache License 2.0](LICENSE-APACHE)
