using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SecretsManager;

/// <summary>
/// Adds a concurrency guard to a provider.
/// </summary>
public sealed class ConcurrentLimiterLayer : ISecretProviderLayer
{
    private readonly ConcurrentLimiterLayerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentLimiterLayer"/> class.
    /// </summary>
    /// <param name="options">Layer options. Defaults to <see cref="ConcurrentLimiterLayerOptions"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="ConcurrentLimiterLayerOptions.MaxConcurrentRequests"/> is not positive.
    /// </exception>
    public ConcurrentLimiterLayer(ConcurrentLimiterLayerOptions? options = null)
    {
        _options = options ?? new ConcurrentLimiterLayerOptions();
        if (_options.MaxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(ConcurrentLimiterLayerOptions.MaxConcurrentRequests));
    }

    /// <summary>
    /// Wraps the given provider so that at most
    /// <see cref="ConcurrentLimiterLayerOptions.MaxConcurrentRequests"/> operations run at once.
    /// </summary>
    /// <param name="inner">The provider to wrap.</param>
    /// <returns>The wrapped provider.</returns>
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new ConcurrentLimiterSecretProvider(inner, _options);
    }

    private sealed class ConcurrentLimiterSecretProvider(ISecretProvider inner, ConcurrentLimiterLayerOptions options)
        : SecretProviderLayerBase(inner)
    {
        private readonly SemaphoreSlim _semaphore = new(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
        private readonly TimeSpan? _acquireTimeout = options.AcquireTimeout;

        protected override async Task<TResult> ExecuteAsync<TResult>(
            string operation,
            string key,
            Func<CancellationToken, Task<TResult>> operationExecutor,
            CancellationToken cancellationToken)
        {
            await AcquireAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operationExecutor(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task AcquireAsync(CancellationToken cancellationToken)
        {
            if (_acquireTimeout is null)
            {
                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!await _semaphore.WaitAsync(_acquireTimeout.Value, cancellationToken).ConfigureAwait(false))
                throw new TimeoutException("Timed out while waiting for concurrency slot.");
        }
    }
}
