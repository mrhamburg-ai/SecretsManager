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

    private sealed class TimeoutSecretProvider(ISecretProvider inner, TimeoutLayerOptions options) : ISecretProvider
    {
        private readonly ISecretProvider _inner = inner;
        private readonly TimeSpan _timeout = options.Timeout;

        public async Task<SecretValue> GetSecretAsync(
            string key,
            SecretQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                return await _inner.GetSecretAsync(key, query, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (IsTimeoutExpired(timeoutCts, cancellationToken))
            {
                throw new TimeoutException($"Timeout reached while executing GetSecretAsync for key '{key}'.");
            }
        }

        public async Task<SecretValue> PutSecretAsync(
            string key,
            string value,
            SecretMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                return await _inner.PutSecretAsync(key, value, metadata, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (IsTimeoutExpired(timeoutCts, cancellationToken))
            {
                throw new TimeoutException($"Timeout reached while executing PutSecretAsync for key '{key}'.");
            }
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                await _inner.DeleteSecretAsync(key, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (IsTimeoutExpired(timeoutCts, cancellationToken))
            {
                throw new TimeoutException($"Timeout reached while executing DeleteSecretAsync for key '{key}'.");
            }
        }

        public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                return await _inner.GetSecretVersionsAsync(key, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (IsTimeoutExpired(timeoutCts, cancellationToken))
            {
                throw new TimeoutException($"Timeout reached while executing GetSecretVersionsAsync for key '{key}'.");
            }
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                return await _inner.SecretExistsAsync(key, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (IsTimeoutExpired(timeoutCts, cancellationToken))
            {
                throw new TimeoutException($"Timeout reached while executing SecretExistsAsync for key '{key}'.");
            }
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private static bool IsTimeoutExpired(
            CancellationTokenSource timeoutCts,
            CancellationToken originalCancellationToken)
        {
            return timeoutCts.IsCancellationRequested && !originalCancellationToken.IsCancellationRequested;
        }
    }
}
