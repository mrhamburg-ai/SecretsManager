---
id: api-reference
title: API Reference
sidebar_label: API Reference
sidebar_position: 5
---

# API Reference

## ISecretProvider

The core interface that all providers implement.

```csharp
public interface ISecretProvider : IAsyncDisposable
{
    Task<SecretValue> GetSecretAsync(
        string key,
        SecretQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<SecretValue> PutSecretAsync(
        string key,
        string value,
        SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<bool> SecretExistsAsync(
        string key,
        CancellationToken cancellationToken = default);
}
```

## Types

### SecretValue

Represents a retrieved secret with its value and metadata.

| Property | Type | Description |
|----------|------|-------------|
| `Key` | `string` | The secret's key/name |
| `Value` | `string` | The secret's value |
| `Version` | `string?` | Version identifier |
| `CreatedAt` | `DateTimeOffset?` | When this version was created |
| `ContentType` | `string?` | MIME type of the value |

### SecretQuery

Options for retrieving secrets.

| Property | Type | Description |
|----------|------|-------------|
| `Version` | `string?` | Specific version to retrieve |
| `Property` | `string?` | JSON dot-path to extract a nested value |

### SecretMetadata

Write-side metadata for creating or updating secrets.

| Property | Type | Description |
|----------|------|-------------|
| `ContentType` | `string?` | MIME type of the value |
| `Tags` | `Dictionary<string, string>?` | Key-value tags/labels |

### SecretVersionInfo

Information about a single version of a secret.

| Property | Type | Description |
|----------|------|-------------|
| `Version` | `string` | Version identifier |
| `CreatedAt` | `DateTimeOffset` | When this version was created |

## Exceptions

### SecretNotFoundException

Thrown when a requested secret does not exist.

```csharp
try
{
    var secret = await provider.GetSecretAsync("nonexistent-key");
}
catch (SecretNotFoundException ex)
{
    Console.WriteLine($"Secret not found: {ex.SecretKey}");
}
```

### SecretProviderException

Base exception for provider-specific errors (network failures, auth errors, etc.).

## SecretProviderBuilder

Fluent builder for configuring and creating providers.

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options => { /* ... */ })    // or WithAzureKeyVault, WithScaleway, WithPostgreSql
    .FromEnvironment()                           // optional: load from env vars
    .FromYamlFile("secretstore.yaml")            // optional: load from YAML
    .Build();                                    // returns ISecretProvider
```

## ISecretProviderLayer

Layers are decorators that wrap providers and can add cross-cutting behavior.

```csharp
public interface ISecretProviderLayer
{
    ISecretProvider Wrap(ISecretProvider inner);
}

public sealed class SecretProviderBuilder
{
    public SecretProviderBuilder AddLayer(ISecretProviderLayer layer);
}
```

### Out-of-box layer extension methods

Layer builders are in the `SecretsManager.Builder` namespace:

```csharp
using SecretsManager;
using SecretsManager.Builder;

var provider = new SecretProviderBuilder()
    .WithFileSystem(options => { /* ... */ })
    .WithConcurrentLimiter()   // controls max concurrent operations
    .WithRetry()               // retries transient failures
    .WithTimeout()             // per-operation timeout
    .WithLogging()             // start/success/failure logging hooks
    .WithAudit()               // durable per-access audit records
    .WithOtelMetrics()         // emits System.Diagnostics.Metrics
    .WithOtelTrace()           // emits OpenTelemetry Activity spans
    .Build();
```

All layers support optional options callbacks, for example:

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options => { /* ... */ })
    .WithRetry(options => options.MaxAttempts = 5)
    .WithTimeout(options => options.Timeout = TimeSpan.FromSeconds(5))
    .WithConcurrentLimiter(options => options.MaxConcurrentRequests = 10)
    .WithOtelMetrics(options => options.MeterName = "secrets-metrics")
    .WithOtelTrace(options => options.ActivitySourceName = "secrets-traces")
    .Build();
```

### Audit types

`AuditLayer` writes a `SecretAuditEvent` to one or more `ISecretAuditSink` instances.

| Type | Description |
|------|-------------|
| `SecretAuditEvent` | Immutable record: operation, provider, key, version, outcome, timestamp, duration, actor, correlation id, trace id, and failure type |
| `SecretAuditOutcome` | `Success`, `Failure`, or `Denied` |
| `SecretAuditScope` | Flags that select reads, writes, deletes, enumeration, or all operations |
| `ISecretAuditSink` | Destination for audit records |
| `ISecretActorAccessor` | Ambient actor lookup, with `AmbientSecretActorAccessor.Instance` as the default |
| `FileSecretAuditSink` | JSON Lines file sink |
| `PostgreSqlSecretAuditSink` | PostgreSQL sink in the `SecretsManager.PostgreSql` package |

See [Audit Trail](advanced/audit-trail.md) for usage.
