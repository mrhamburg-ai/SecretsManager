using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SecretsManager;

/// <summary>
/// Enforces a timeout per provider operation.
/// </summary>
public sealed class TimeoutLayer : ISecretProviderLayer
{
    private readonly TimeoutLayerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutLayer"/> class.
    /// </summary>
    /// <param name="options">Layer options, including the timeout. Defaults to <see cref="TimeoutLayerOptions"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="TimeoutLayerOptions.Timeout"/> is not positive.
    /// </exception>
    public TimeoutLayer(TimeoutLayerOptions? options = null)
    {
        _options = options ?? new TimeoutLayerOptions();

        if (_options.Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TimeoutLayerOptions.Timeout));
    }

    /// <summary>
    /// Wraps the given provider so that every operation is cancelled after
    /// <see cref="TimeoutLayerOptions.Timeout"/> and surfaced as a <see cref="TimeoutException"/>.
    /// </summary>
    /// <param name="inner">The provider to wrap.</param>
    /// <returns>The wrapped provider.</returns>
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new TimeoutSecretProvider(inner, _options);
    }

    private sealed class TimeoutSecretProvider(ISecretProvider inner, TimeoutLayerOptions options)
        : SecretProviderLayerBase(inner)
    {
        private readonly TimeSpan _timeout = options.Timeout;

        protected override async Task<TResult> ExecuteAsync<TResult>(
            string operation,
            string key,
            Func<CancellationToken, Task<TResult>> operationExecutor,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                return await operationExecutor(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (IsTimeoutExpired(timeoutCts, cancellationToken))
            {
                throw new TimeoutException(
                    $"Timeout reached while executing {operation} for key '{key}'.");
            }
        }

        private static bool IsTimeoutExpired(
            CancellationTokenSource timeoutCts,
            CancellationToken originalCancellationToken)
        {
            return timeoutCts.IsCancellationRequested && !originalCancellationToken.IsCancellationRequested;
        }
    }
}
