namespace SecretsManager;

/// <summary>
/// Options for <see cref="RetryLayer"/>.
/// </summary>
public sealed class RetryLayerOptions
{
    /// <summary>
    /// Maximum number of attempts, including the initial attempt.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries before applying exponential backoff.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum delay between retry attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Jitter multiplier applied on each delay, between 0 and 1.
    /// </summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Optional predicate that determines if a failure should be retried.
    /// Defaults to retrying all non-cancellation exceptions.
    /// </summary>
    public Func<Exception, bool>? RetryPredicate { get; set; }

    /// <summary>
    /// Optional logging sink that receives one event for each retry.
    /// When null, the builder shares the logger from a configured <see cref="LoggingLayer"/>.
    /// </summary>
    public ISecretProviderLogger? Logger { get; set; }
}
