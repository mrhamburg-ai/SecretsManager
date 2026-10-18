---
id: layering
title: Layering and Decorators
sidebar_label: Layering
sidebar_position: 2
---

# Layering and Decorators

You can compose additional behavior around any `ISecretProvider` by using layers.

Layers are decorators that wrap a provider implementation and add cross-cutting behavior without changing
the provider itself.

`SecretProviderBuilder` applies layers in the same order they are added.

```csharp
using SecretsManager;
using SecretsManager.Builder;

var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/etc/secrets";
    })
    .WithRetry()
    .WithTimeout()
    .WithLogging(options => options.OperationPrefix = "app")
    .Build();
```

In this example, `Retry` is applied first, then `Timeout`, then `Logging`, and finally the base provider.

## Why layering?

- Keep providers focused while adding resiliency and observability once.
- Build predictable call behavior across environments.
- Share the same policy across provider types.
- Reduce boilerplate for retries, throttling, tracing, and diagnostics.

## Built-in layers

The core package ships these decorators:

- Concurrent limiter: `WithConcurrentLimiter`
- Retry: `WithRetry`
- Timeout: `WithTimeout`
- Logging: `WithLogging`
- OpenTelemetry metrics: `WithOtelMetrics`
- OpenTelemetry trace: `WithOtelTrace`

## Concurrent limiter

Limits how many provider operations run at the same time.

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options => options.BasePath = "/etc/secrets")
    .WithConcurrentLimiter(options =>
    {
        options.MaxConcurrentRequests = 5;
        options.AcquireTimeout = TimeSpan.FromMilliseconds(200);
    })
    .Build();
```

Options:

- `MaxConcurrentRequests`: maximum concurrent operations (default `10`).
- `AcquireTimeout`: wait timeout before failing with `TimeoutException` (`null` means wait forever).

## Retry

Retries failed provider calls with exponential backoff.

```csharp
var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
    })
    .WithRetry(options =>
    {
        options.MaxAttempts = 5;
        options.BaseDelay = TimeSpan.FromMilliseconds(100);
        options.MaxDelay = TimeSpan.FromSeconds(2);
        options.JitterFactor = 0.2;
    })
    .Build();
```

Defaults:

- Does not retry `SecretNotFoundException`.
- Does not retry canceled operations.
- `MaxAttempts` includes the first attempt.
- `RetryPredicate` can override retry behavior.

## Timeout

Applies a cancellation deadline per operation.

```csharp
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "my-project";
        options.CredentialsPath = "/etc/secrets/gcp.json";
    })
    .WithTimeout(options =>
    {
        options.Timeout = TimeSpan.FromSeconds(5);
    })
    .Build();
```

Default timeout is `10` seconds.

## Logging

Emits lifecycle events for each operation.

```csharp
var provider = new SecretProviderBuilder()
    .WithScaleway(options =>
    {
        options.Region = "fr-par";
        options.ProjectId = "...";
    })
    .WithLogging(options =>
    {
        options.OperationPrefix = "secret";
        options.Logger = new MyLogger();
    })
    .Build();
```

`ISecretProviderLogger` interface:

```csharp
public interface ISecretProviderLogger
{
    void LogStart(SecretProviderLogContext context);
    void LogSuccess(SecretProviderLogContext context, TimeSpan duration);
    void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration);
}
```

`OperationPrefix` prepends operation names (for example `secret.GetSecretAsync`).

If no logger is provided, `NullSecretProviderLogger` is used.

## OpenTelemetry metrics

Generates metrics using `System.Diagnostics.Metrics`.

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(options => options.BasePath = "/etc/secrets")
    .WithOtelMetrics(options =>
    {
        options.MeterName = "secrets.metrics";
        options.MeterVersion = "1.0.0";
        options.MetricPrefix = "secrets.";
    })
    .Build();
```

Emitted metric names are prefixed by `MetricPrefix`, for example:

- `secrets.operation.calls`
- `secrets.operation.failures`
- `secrets.operation.duration_ms`
- `secrets.operation.in_flight`

## OpenTelemetry trace

Adds `Activity` spans for all operations.

```csharp
var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "us-east-1";
        options.AccessKey = "AKIA...";
        options.SecretKey = "...";
    })
    .WithOtelTrace(options =>
    {
        options.ActivitySourceName = "my-secrets-traces";
        options.RecordException = true;
    })
    .Build();
```

Span tags include:

- `secrets.operation`
- `secrets.key`

## Custom layers

You can create your own decorator by implementing `ISecretProviderLayer` and wrapping `ISecretProvider`.

```csharp
public sealed class AuditLayer : ISecretProviderLayer
{
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        return new AuditProvider(inner);
    }

    private sealed class AuditProvider(ISecretProvider inner) : ISecretProvider
    {
        public Task<SecretValue> GetSecretAsync(string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
            => inner.GetSecretAsync(key, query, cancellationToken);

        public Task<SecretValue> PutSecretAsync(string key, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default)
            => inner.PutSecretAsync(key, value, metadata, cancellationToken);

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
            => inner.DeleteSecretAsync(key, cancellationToken);

        public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(string key, CancellationToken cancellationToken = default)
            => inner.GetSecretVersionsAsync(key, cancellationToken);

        public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
            => inner.SecretExistsAsync(key, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
```

Then call it via `AddLayer`:

```csharp
var provider = new SecretProviderBuilder()
    .WithFileSystem(opts => opts.BasePath = "/etc/secrets")
    .AddLayer(new AuditLayer())
    .Build();
```

Or create a small extension method in your own project.

## Why some behaviour cannot be a layer

Layers wrap a finished provider, so they can change what a call *does*, but not what the provider *is*. Encryption is the clearest example.

`SecretProviderBuilder.Build()` constructs the provider first and wraps it afterwards:

```csharp
var provider = factory.Create(_configuration.Settings);   // options read here

for (var i = _layers.Count - 1; i >= 0; i--)
    provider = _layers[i].Wrap(provider);                 // layers wrap AFTER
```

A layer receives only the finished provider. The key *source* is a property of the provider's options, and the provider's options are not reachable from the wrapped interface — the layer has no reference to them, or to the provider's internals. A decorator can intercept calls, but it cannot reach back into the configuration that produced the provider.

The only way a layer could work is if the provider exposed its key source for replacement — which would mean publishing key material through a public API, the opposite of what encryption is for.

**Sourcing the key from another provider therefore lives in the providers' key resolution**, not in the layer pipeline. See [FileSystem Provider](providers/filesystem.md#with-the-key-from-another-provider) and [PostgreSql Provider](providers/postgresql.md#with-the-key-from-another-provider) for `EncryptionKeyProvider` and `SecretProviderKeySource`.

:::note
The key itself is resolved lazily, on first use, rather than at construction time — a key held in another provider may require a network round trip. That does not make it layerable: the *source* of the key is fixed when the provider is built, which is before any layer runs.
:::
