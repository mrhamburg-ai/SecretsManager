using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SecretsManager.Internal;

namespace SecretsManager;

/// <summary>
/// Adds logging hooks around provider operations.
/// </summary>
public sealed class LoggingLayer : ISecretProviderLayer, IProviderNameAwareLayer
{
    private readonly LoggingLayerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingLayer"/> class.
    /// </summary>
    /// <param name="options">Layer options, including the log sink. Defaults to <see cref="LoggingLayerOptions"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <see cref="LoggingLayerOptions.Logger"/> is null.
    /// </exception>
    public LoggingLayer(LoggingLayerOptions? options = null)
    {
        _options = options ?? new LoggingLayerOptions();
        if (_options.Logger is null)
            throw new ArgumentNullException(nameof(LoggingLayerOptions.Logger));
    }

    /// <summary>
    /// Wraps the given provider so that every operation is reported to the configured
    /// <see cref="ISecretProviderLogger"/>.
    /// </summary>
    /// <param name="inner">The provider to wrap.</param>
    /// <returns>The wrapped provider.</returns>
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new LoggingSecretProvider(inner, _options);
    }

    void IProviderNameAwareLayer.SetProviderName(string providerName)
    {
        _options.ProviderName = providerName;
    }

    internal LoggingLayerOptions Options => _options;

    private sealed class LoggingSecretProvider(ISecretProvider inner, LoggingLayerOptions options) : ISecretProvider
    {
        private readonly ISecretProvider _inner = inner;
        private readonly ISecretProviderLogger _logger = options.Logger;
        private readonly LoggingLayerOptions _options = options;
        private readonly string _operationPrefix = string.IsNullOrWhiteSpace(options.OperationPrefix)
            ? string.Empty
            : options.OperationPrefix + ".";

        public async Task<SecretValue> GetSecretAsync(
            string key,
            SecretQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                () => _inner.GetSecretAsync(key, query, cancellationToken),
                (timestamp, outcome) => CreateContext(
                    "GetSecretAsync", key, isMetadataWrite: false, timestamp, outcome));
        }

        public async Task<SecretValue> PutSecretAsync(
            string key,
            string value,
            SecretMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                () => _inner.PutSecretAsync(key, value, metadata, cancellationToken),
                (timestamp, outcome) => CreateContext(
                    "PutSecretAsync", key, metadata is not null, timestamp, outcome));
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(
                () => _inner.DeleteSecretAsync(key, cancellationToken),
                (timestamp, outcome) => CreateContext(
                    "DeleteSecretAsync", key, isMetadataWrite: false, timestamp, outcome));
        }

        public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                () => _inner.GetSecretVersionsAsync(key, cancellationToken),
                (timestamp, outcome) => CreateContext(
                    "GetSecretVersionsAsync", key, isMetadataWrite: false, timestamp, outcome));
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                () => _inner.SecretExistsAsync(key, cancellationToken),
                (timestamp, outcome) => CreateContext(
                    "SecretExistsAsync", key, isMetadataWrite: false, timestamp, outcome));
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<DateTimeOffset, SecretProviderLogOutcome?, SecretProviderLogContext> createContext)
        {
            var started = Stopwatch.GetTimestamp();
            var timestamp = DateTimeOffset.UtcNow;

            if (IsEnabled(SecretProviderLogLevel.Debug))
                SecretProviderLogging.TryLog(() => _logger.LogStart(createContext(timestamp, null)));

            T result;
            try
            {
                result = await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (IsEnabled(SecretProviderLogLevel.Error))
                    SecretProviderLogging.TryLog(() => _logger.LogFailure(
                        createContext(timestamp, SecretProviderLogOutcome.Failure), ex, GetElapsed(started)));
                throw;
            }

            if (IsEnabled(SecretProviderLogLevel.Debug))
                SecretProviderLogging.TryLog(() => _logger.LogSuccess(
                    createContext(timestamp, SecretProviderLogOutcome.Success), GetElapsed(started)));
            return result;
        }

        private async Task ExecuteAsync(
            Func<Task> operation,
            Func<DateTimeOffset, SecretProviderLogOutcome?, SecretProviderLogContext> createContext)
        {
            var started = Stopwatch.GetTimestamp();
            var timestamp = DateTimeOffset.UtcNow;

            if (IsEnabled(SecretProviderLogLevel.Debug))
                SecretProviderLogging.TryLog(() => _logger.LogStart(createContext(timestamp, null)));

            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (IsEnabled(SecretProviderLogLevel.Error))
                    SecretProviderLogging.TryLog(() => _logger.LogFailure(
                        createContext(timestamp, SecretProviderLogOutcome.Failure), ex, GetElapsed(started)));
                throw;
            }

            if (IsEnabled(SecretProviderLogLevel.Debug))
                SecretProviderLogging.TryLog(() => _logger.LogSuccess(
                    createContext(timestamp, SecretProviderLogOutcome.Success), GetElapsed(started)));
        }

        private bool IsEnabled(SecretProviderLogLevel level)
        {
            var enabled = false;
            SecretProviderLogging.TryLog(() => enabled = _logger.IsEnabled(level));
            return enabled;
        }

        private SecretProviderLogContext CreateContext(
            string operation,
            string? key,
            bool isMetadataWrite,
            DateTimeOffset timestamp,
            SecretProviderLogOutcome? outcome)
        {
            return new SecretProviderLogContext
            {
                Operation = ComposeOperation(operation),
                Key = key,
                IsMetadataWrite = isMetadataWrite,
                Provider = _options.ProviderName ?? string.Empty,
                Outcome = outcome,
                Timestamp = timestamp
            };
        }

        private TimeSpan GetElapsed(long startTimestamp)
        {
            var end = Stopwatch.GetTimestamp();
            return TimeSpan.FromSeconds((double)(end - startTimestamp) / Stopwatch.Frequency);
        }

        private string ComposeOperation(string operation)
        {
            return _operationPrefix + operation;
        }
    }
}
