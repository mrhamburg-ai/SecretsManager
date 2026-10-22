---
id: logging
title: Logging
sidebar_label: Logging
sidebar_position: 4
---

# Logging

`LoggingLayer` reports every secret operation to an `ISecretProviderLogger`. The built-in
`LoggerSecretProviderLogger` adapts that interface to `Microsoft.Extensions.Logging`, so secret
operations appear in the same sinks as the rest of your application (console, Seq, Elastic, Loki,
OpenTelemetry log export, and so on).

Logging lives in a layer, not in the providers. No provider constructor takes a logger, so all 14
provider packages keep their public API. The layer also sees pipeline behavior that a provider
cannot see, such as retries.

## Quick start

```csharp
using Microsoft.Extensions.Logging;
using SecretsManager;
using SecretsManager.Builder;

ILogger logger = loggerFactory.CreateLogger("SecretsManager");

await using var provider = new SecretProviderBuilder()
    .WithFileSystem(options => options.BasePath = "/var/secrets")
    .WithLogging(options =>
    {
        options.Logger = new LoggerSecretProviderLogger(logger);
        options.OperationPrefix = "app";
    })
    .Build();
```

## ASP.NET Core

`AddSecretsManager()` registers an `ISecretProviderLogger` that writes through the application's
`ILoggerFactory` (category `SecretsManager`). When no `ILoggerFactory` is registered, it falls back
to a no-op logger. A logger that you register yourself is never replaced.

```csharp
builder.Services.AddLogging();
builder.Services.AddSecretsManager();

builder.Services.AddSingleton<ISecretProvider>(sp =>
{
    var secrets = sp.GetRequiredService<SecretProviderBuilder>();

    return secrets
        .WithFileSystem(options => options.BasePath = "/var/secrets")
        .WithRetry()
        .WithLogging(sp.GetRequiredService<ISecretProviderLogger>())
        .Build();
});
```

## Log levels

The adapter honors the minimum level of your logging configuration. The layer asks
`IsEnabled` first, so when a level is filtered out it builds no context and makes no logger call.

| Event | `SecretProviderLogLevel` | MEL `LogLevel` |
| --- | --- | --- |
| Operation start | `Debug` | `Debug` |
| Operation success | `Debug` | `Debug` |
| Retry | `Warning` | `Warning` |
| Operation failure | `Error` | `Error` |

## Log context

Every event receives a `SecretProviderLogContext`.

| Field | Description |
| --- | --- |
| `Operation` | Operation name, for example `GetSecretAsync`, with the optional `OperationPrefix` |
| `Key` | Secret key |
| `IsMetadataWrite` | Whether a write carries metadata |
| `Provider` | Provider name, for example `vault`. The builder sets it on every layer |
| `Outcome` | `Success` or `Failure`. It is `null` for the start event |
| `Timestamp` | When the operation started |

The MEL adapter writes `Operation`, `Key` and `Provider` as structured properties, plus
`ElapsedMs` for success and failure, and `Attempt` and `DelayMs` for retries.

## Retries

When a `RetryLayer` retries a read, the logger receives a `LogRetry` event with the next attempt
number and the delay. Add `WithLogging` and `WithRetry` in either order. The builder shares the
logging layer's logger with the retry layer. Set `RetryLayerOptions.Logger` to use a different one.

## Failure isolation

Logging is best-effort. A logger that throws, in any method including `IsEnabled`, cannot:

- fail a secret operation,
- turn a successful read into a failure,
- hide the original exception of a failed operation.

The layer swallows logger exceptions.

## Secrets and redaction

- `SecretProviderLogContext` has no value field, and secret values are never logged.
- HTTP-based providers do not copy the response body into exception messages. Error messages
  contain only the operation, HTTP status code and reason phrase, because backends can echo
  submitted values in error payloads.
- If an exception from `PutSecretAsync` still contains the submitted value, the layer replaces it
  with `[REDACTED]` before it is logged. The caller still receives the original exception.

Exception messages that come from other sources are logged as they are. Treat your log sink as
sensitive.

## Custom loggers

Implement `ISecretProviderLogger` to send events to another destination:

```csharp
public interface ISecretProviderLogger
{
    bool IsEnabled(SecretProviderLogLevel level);
    void LogStart(SecretProviderLogContext context);
    void LogSuccess(SecretProviderLogContext context, TimeSpan duration);
    void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration);
    void LogRetry(SecretProviderLogContext context, int attempt, TimeSpan delay);
}
```

`NullSecretProviderLogger` is the default and emits nothing.
