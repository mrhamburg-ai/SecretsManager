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

    private sealed class ConcurrentLimiterSecretProvider(ISecretProvider inner, ConcurrentLimiterLayerOptions options) : ISecretProvider
    {
        private readonly ISecretProvider _inner = inner;
        private readonly SemaphoreSlim _semaphore = new(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
        private readonly TimeSpan? _acquireTimeout = options.AcquireTimeout;

        public async Task<SecretValue> GetSecretAsync(
            string key,
            SecretQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            await AcquireAsync(cancellationToken);
            try
            {
                return await _inner.GetSecretAsync(key, query, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<SecretValue> PutSecretAsync(
            string key,
            string value,
            SecretMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            await AcquireAsync(cancellationToken);
            try
            {
                return await _inner.PutSecretAsync(key, value, metadata, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            await AcquireAsync(cancellationToken);
            try
            {
                await _inner.DeleteSecretAsync(key, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            await AcquireAsync(cancellationToken);
            try
            {
                return await _inner.GetSecretVersionsAsync(key, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            await AcquireAsync(cancellationToken);
            try
            {
                return await _inner.SecretExistsAsync(key, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            _semaphore.Dispose();
            return _inner.DisposeAsync();
        }

        private async Task AcquireAsync(CancellationToken cancellationToken)
        {
            if (_acquireTimeout is null)
            {
                await _semaphore.WaitAsync(cancellationToken);
                return;
            }

            if (!await _semaphore.WaitAsync(_acquireTimeout.Value, cancellationToken))
                throw new TimeoutException("Timed out while waiting for concurrency slot.");
        }
    }
}
