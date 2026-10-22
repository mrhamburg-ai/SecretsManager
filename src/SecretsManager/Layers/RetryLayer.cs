using SecretsManager.Internal;

namespace SecretsManager;

/// <summary>
/// Retries failed operations with exponential backoff.
/// </summary>
/// <remarks>
/// Only transient failures are retried by default. <see cref="ISecretProvider.PutSecretAsync"/>
/// is never retried because writes are not idempotent. Configure <see cref="RetryLayerOptions.RetryPredicate"/>
/// to override the default classification.
/// </remarks>
public sealed class RetryLayer : ISecretProviderLayer, IProviderNameAwareLayer, ISecretProviderLoggerAwareLayer
{
    private readonly RetryLayerOptions _options;
    private ISecretProviderLogger _logger;
    private string _providerName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLayer"/> class.
    /// </summary>
    /// <param name="options">Layer options. Defaults to <see cref="RetryLayerOptions"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="RetryLayerOptions.MaxAttempts"/>, <see cref="RetryLayerOptions.MaxDelay"/>,
    /// <see cref="RetryLayerOptions.BaseDelay"/> or <see cref="RetryLayerOptions.JitterFactor"/> is out of range.
    /// </exception>
    public RetryLayer(RetryLayerOptions? options = null)
    {
        _options = options ?? new RetryLayerOptions();
        _logger = _options.Logger ?? new NullSecretProviderLogger();

        if (_options.MaxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(RetryLayerOptions.MaxAttempts));

        if (_options.MaxDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryLayerOptions.MaxDelay));

        if (_options.BaseDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryLayerOptions.BaseDelay));

        if (_options.JitterFactor is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(RetryLayerOptions.JitterFactor));
    }

    /// <summary>
    /// Wraps the given provider so that transient failures are retried with exponential backoff
    /// and optional jitter.
    /// </summary>
    /// <param name="inner">The provider to wrap.</param>
    /// <returns>The wrapped provider.</returns>
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new RetrySecretProvider(inner, _options, _logger, _providerName);
    }

    void IProviderNameAwareLayer.SetProviderName(string providerName)
    {
        _providerName = providerName;
    }

    void ISecretProviderLoggerAwareLayer.SetLogger(ISecretProviderLogger logger)
    {
        if (_options.Logger is null)
            _logger = logger;
    }

    private sealed class RetrySecretProvider(
        ISecretProvider inner,
        RetryLayerOptions options,
        ISecretProviderLogger logger,
        string providerName) : ISecretProvider
    {
        public async Task<SecretValue> GetSecretAsync(
            string key,
            SecretQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                token => inner.GetSecretAsync(key, query, token),
                cancellationToken,
                "GetSecretAsync",
                key);
        }

        public Task<SecretValue> PutSecretAsync(
            string key,
            string value,
            SecretMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            // Writes are not idempotent: retrying may create duplicate versions or
            // overwrite a value that was already stored by a partially successful attempt.
            return inner.PutSecretAsync(key, value, metadata, cancellationToken);
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(
                token => inner.DeleteSecretAsync(key, token),
                cancellationToken,
                "DeleteSecretAsync",
                key);
        }

        public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                token => inner.GetSecretVersionsAsync(key, token),
                cancellationToken,
                "GetSecretVersionsAsync",
                key);
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                token => inner.SecretExistsAsync(key, token),
                cancellationToken,
                "SecretExistsAsync",
                key);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        private async Task ExecuteAsync(
            Func<CancellationToken, Task> operationExecutor,
            CancellationToken cancellationToken,
            string operation,
            string key)
        {
            await ExecuteAsync<object>(
                async token =>
                {
                    await operationExecutor(token);
                    return null!;
                },
                cancellationToken,
                operation,
                key);
        }

        private async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operationExecutor,
            CancellationToken cancellationToken,
            string operation,
            string key)
        {
            var attempt = 1;
            var startedAt = DateTimeOffset.UtcNow;

            while (true)
            {
                try
                {
                    return await operationExecutor(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ShouldRetry(ex) && attempt < options.MaxAttempts)
                {
                    attempt++;
                    var delay = CalculateDelay(attempt);
                    LogRetry(operation, key, attempt, delay, startedAt);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        private void LogRetry(
            string operation,
            string key,
            int attempt,
            TimeSpan delay,
            DateTimeOffset startedAt)
        {
            if (!IsEnabled(SecretProviderLogLevel.Warning))
                return;

            var context = new SecretProviderLogContext
            {
                Operation = operation,
                Key = key,
                IsMetadataWrite = false,
                Provider = providerName,
                Outcome = SecretProviderLogOutcome.Failure,
                Timestamp = startedAt
            };

            SecretProviderLogging.TryLog(() => logger.LogRetry(context, attempt, delay));
        }

        private bool IsEnabled(SecretProviderLogLevel level)
        {
            var enabled = false;
            SecretProviderLogging.TryLog(() => enabled = logger.IsEnabled(level));
            return enabled;
        }

        private bool ShouldRetry(Exception ex)
        {
            if (options.RetryPredicate is not null)
                return options.RetryPredicate(ex);

            return IsTransient(ex);
        }

        private static bool IsTransient(Exception ex) => ex switch
        {
            OperationCanceledException => false,
            SecretNotFoundException => false,
            ArgumentException => false,
            UnauthorizedAccessException => false,
            NotSupportedException => false,
            TimeoutException => true,
            HttpRequestException => true,
            IOException => true,
            System.Net.Sockets.SocketException => true,
            SecretProviderException { InnerException: { } inner } => IsTransient(inner),
            SecretProviderException => true,
            AggregateException aggregate => aggregate.InnerExceptions.Any(IsTransient),
            _ => false,
        };

        private TimeSpan CalculateDelay(int attempt)
        {
            if (attempt <= 1)
                return options.BaseDelay;

            var exponent = Math.Min(10, attempt - 1);
            var exponential = Math.Min(
                options.MaxDelay.TotalMilliseconds,
                options.BaseDelay.TotalMilliseconds * Math.Pow(2, exponent));

            if (options.JitterFactor <= 0)
                return TimeSpan.FromMilliseconds(exponential);

            var jitterAmount = exponential * options.JitterFactor;
            var jitter = (Random.Shared.NextDouble() * 2 - 1) * jitterAmount;
            var value = Math.Max(0, exponential + jitter);
            return TimeSpan.FromMilliseconds(value);
        }
    }
}
