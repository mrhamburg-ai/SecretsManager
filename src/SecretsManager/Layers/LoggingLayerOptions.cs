using System;

namespace SecretsManager;

/// <summary>
/// Options for <see cref="LoggingLayer"/>.
/// </summary>
public sealed class LoggingLayerOptions
{
    /// <summary>
    /// Logging sink implementation. Defaults to <see cref="NullSecretProviderLogger"/>.
    /// </summary>
    public ISecretProviderLogger Logger { get; set; } = new NullSecretProviderLogger();

    /// <summary>
    /// Optional operation name prefix for logs.
    /// </summary>
    public string OperationPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Name of the provider that produced the logged operations, when known.
    /// </summary>
    public string? ProviderName { get; set; }
}

/// <summary>
/// Severity levels used by <see cref="ISecretProviderLogger"/>.
/// </summary>
public enum SecretProviderLogLevel
{
    /// <summary>
    /// Fine-grained diagnostic events, including operation start and success.
    /// </summary>
    Debug,

    /// <summary>
    /// Informational events.
    /// </summary>
    Information,

    /// <summary>
    /// Events that indicate a potential problem.
    /// </summary>
    Warning,

    /// <summary>
    /// Events that indicate a failed operation.
    /// </summary>
    Error
}

/// <summary>
/// Outcome of a logged provider operation.
/// </summary>
public enum SecretProviderLogOutcome
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The operation failed.
    /// </summary>
    Failure
}

/// <summary>
/// Logging hook used by <see cref="LoggingLayer"/>.
/// </summary>
public interface ISecretProviderLogger
{
    /// <summary>
    /// Determines whether events at the given level should be emitted.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when events at the level are enabled.</returns>
    bool IsEnabled(SecretProviderLogLevel level);

    /// <summary>
    /// Called immediately before a provider operation runs.
    /// </summary>
    void LogStart(SecretProviderLogContext context);

    /// <summary>
    /// Called when an operation finishes successfully.
    /// </summary>
    void LogSuccess(SecretProviderLogContext context, TimeSpan duration);

    /// <summary>
    /// Called when an operation throws.
    /// </summary>
    void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration);

    /// <summary>
    /// Called when an operation is retried.
    /// </summary>
    /// <param name="context">Context of the operation being retried.</param>
    /// <param name="attempt">The upcoming attempt number.</param>
    /// <param name="delay">How long to wait before the retry.</param>
    void LogRetry(SecretProviderLogContext context, int attempt, TimeSpan delay);
}

/// <summary>
/// Context object passed to logging sinks.
/// </summary>
public sealed class SecretProviderLogContext
{
    /// <summary>
    /// Name of the operation being executed (for example <c>GetSecretAsync</c>), prefixed with
    /// <see cref="LoggingLayerOptions.OperationPrefix"/> when one is configured.
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// The secret key the operation applies to, when the operation targets a single secret.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Whether the operation writes metadata rather than a secret value.
    /// </summary>
    public bool IsMetadataWrite { get; init; }

    /// <summary>
    /// Name of the provider that produced the operation, when known.
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Outcome of the operation, or <see langword="null"/> while it is still running.
    /// </summary>
    public SecretProviderLogOutcome? Outcome { get; init; }

    /// <summary>
    /// When the operation started.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Default no-op logger used by default.
/// </summary>
public sealed class NullSecretProviderLogger : ISecretProviderLogger
{
    /// <summary>
    /// Always returns <see langword="false"/> because this logger emits nothing.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="false"/>.</returns>
    public bool IsEnabled(SecretProviderLogLevel level) => false;

    /// <summary>
    /// Does nothing.
    /// </summary>
    /// <param name="context">Context of the operation that is about to run.</param>
    public void LogStart(SecretProviderLogContext context)
    {
    }

    /// <summary>
    /// Does nothing.
    /// </summary>
    /// <param name="context">Context of the completed operation.</param>
    /// <param name="duration">How long the operation took.</param>
    public void LogSuccess(SecretProviderLogContext context, TimeSpan duration)
    {
    }

    /// <summary>
    /// Does nothing.
    /// </summary>
    /// <param name="context">Context of the failed operation.</param>
    /// <param name="exception">The exception thrown by the operation.</param>
    /// <param name="duration">How long the operation ran before failing.</param>
    public void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration)
    {
    }

    /// <summary>
    /// Does nothing.
    /// </summary>
    /// <param name="context">Context of the operation being retried.</param>
    /// <param name="attempt">The upcoming attempt number.</param>
    /// <param name="delay">How long to wait before the retry.</param>
    public void LogRetry(SecretProviderLogContext context, int attempt, TimeSpan delay)
    {
    }
}
