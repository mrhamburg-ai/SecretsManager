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
}

/// <summary>
/// Logging hook used by <see cref="LoggingLayer"/>.
/// </summary>
public interface ISecretProviderLogger
{
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
}

/// <summary>
/// Default no-op logger used by default.
/// </summary>
public sealed class NullSecretProviderLogger : ISecretProviderLogger
{
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
}
