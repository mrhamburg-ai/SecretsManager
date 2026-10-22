using System;
using Microsoft.Extensions.Logging;
using SecretsManager.Internal;

namespace SecretsManager;

/// <summary>
/// Adapts an <see cref="ILogger"/> to the <see cref="ISecretProviderLogger"/> contract.
/// </summary>
public sealed class LoggerSecretProviderLogger : ISecretProviderLogger
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerSecretProviderLogger"/> class.
    /// </summary>
    /// <param name="logger">The Microsoft.Extensions.Logging logger to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    public LoggerSecretProviderLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Determines whether events at the given level should be emitted.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when events at the level are enabled.</returns>
    public bool IsEnabled(SecretProviderLogLevel level) => _logger.IsEnabled(Map(level));

    /// <summary>
    /// Called immediately before a provider operation runs.
    /// </summary>
    /// <param name="context">Context of the operation that is about to run.</param>
    public void LogStart(SecretProviderLogContext context) =>
        _logger.LogDebug(
            "Secret operation {Operation} starting for key {Key} in provider {Provider}",
            context.Operation, context.Key, context.Provider);

    /// <summary>
    /// Called when an operation finishes successfully.
    /// </summary>
    /// <param name="context">Context of the completed operation.</param>
    /// <param name="duration">How long the operation took.</param>
    public void LogSuccess(SecretProviderLogContext context, TimeSpan duration) =>
        _logger.LogDebug(
            "Secret operation {Operation} succeeded for key {Key} in provider {Provider} after {ElapsedMs} ms",
            context.Operation, context.Key, context.Provider, duration.TotalMilliseconds);

    /// <summary>
    /// Called when an operation throws.
    /// </summary>
    /// <param name="context">Context of the failed operation.</param>
    /// <param name="exception">The exception that ended the operation.</param>
    /// <param name="duration">How long the operation ran before failing.</param>
    public void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration)
    {
        var failureType = exception is RedactedSecretProviderException redacted
            ? redacted.OriginalTypeName
            : exception.GetType().FullName;

        _logger.LogError(
            exception,
            "Secret operation {Operation} failed for key {Key} in provider {Provider} after {ElapsedMs} ms. FailureType={FailureType}",
            context.Operation, context.Key, context.Provider, duration.TotalMilliseconds, failureType);
    }

    /// <summary>
    /// Called when an operation is retried.
    /// </summary>
    /// <param name="context">Context of the operation being retried.</param>
    /// <param name="attempt">The upcoming attempt number.</param>
    /// <param name="delay">How long to wait before the retry.</param>
    public void LogRetry(SecretProviderLogContext context, int attempt, TimeSpan delay) =>
        _logger.LogWarning(
            "Secret operation {Operation} retrying for key {Key} in provider {Provider}: attempt {Attempt} after {DelayMs} ms",
            context.Operation, context.Key, context.Provider, attempt, delay.TotalMilliseconds);

    private static LogLevel Map(SecretProviderLogLevel level) => level switch
    {
        SecretProviderLogLevel.Debug => LogLevel.Debug,
        SecretProviderLogLevel.Information => LogLevel.Information,
        SecretProviderLogLevel.Warning => LogLevel.Warning,
        SecretProviderLogLevel.Error => LogLevel.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };
}
