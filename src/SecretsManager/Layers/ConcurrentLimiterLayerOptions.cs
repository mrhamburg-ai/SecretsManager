using System;

namespace SecretsManager;

/// <summary>
/// Options for <see cref="ConcurrentLimiterLayer"/>.
/// </summary>
public sealed class ConcurrentLimiterLayerOptions
{
    /// <summary>
    /// Maximum number of concurrent operations allowed.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Optional timeout for acquiring a concurrency slot.
    /// When null, wait indefinitely.
    /// </summary>
    public TimeSpan? AcquireTimeout { get; set; }
}
