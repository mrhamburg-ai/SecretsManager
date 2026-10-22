using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace SecretsManager;

/// <summary>
/// Emits System.Diagnostics metrics for provider operations.
/// </summary>
public sealed class OtelMetricsLayer : ISecretProviderLayer
{
    private readonly OtelMetricsLayerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtelMetricsLayer"/> class.
    /// </summary>
    /// <param name="options">Layer options, including the meter name. Defaults to <see cref="OtelMetricsLayerOptions"/>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="OtelMetricsLayerOptions.MeterName"/> is empty.
    /// </exception>
    public OtelMetricsLayer(OtelMetricsLayerOptions? options = null)
    {
        _options = options ?? new OtelMetricsLayerOptions();

        if (string.IsNullOrWhiteSpace(_options.MeterName))
            throw new ArgumentException("MeterName is required.", nameof(OtelMetricsLayerOptions.MeterName));
    }

    /// <summary>
    /// Wraps the given provider so that calls, failures, in-flight operations and durations are
    /// recorded as <see cref="System.Diagnostics.Metrics"/> instruments.
    /// </summary>
    /// <param name="inner">The provider to wrap.</param>
    /// <returns>The wrapped provider.</returns>
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new OtelMetricsSecretProvider(inner, _options);
    }

    private sealed class OtelMetricsSecretProvider : ISecretProvider
    {
        private readonly ISecretProvider _inner;
        private readonly Meter _meter;
        private readonly Counter<long> _callCounter;
        private readonly Counter<long> _failureCounter;
        private readonly Histogram<double> _durationMs;
        private readonly UpDownCounter<long> _inFlight;
        private readonly string _metricPrefix;
        private static readonly double TimestampFrequency = 1000d / Stopwatch.Frequency;
        private static readonly ConcurrentDictionary<string, Meter> SharedMeters =
            new(StringComparer.Ordinal);

        public OtelMetricsSecretProvider(ISecretProvider inner, OtelMetricsLayerOptions options)
        {
            _inner = inner;
            // Meters are process-wide and must not be disposed per wrapper; reuse a single
            // instance so repeated Wrap calls do not leak duplicate meters.
            var meterKey = options.MeterName + "\u0000" + options.MeterVersion;
            _meter = SharedMeters.GetOrAdd(
                meterKey,
                _ => new Meter(options.MeterName, options.MeterVersion));
            _metricPrefix = NormalizeMetricPrefix(options.MetricPrefix);

            _callCounter = _meter.CreateCounter<long>(
                name: ComposeMetricName("operation.calls"),
                unit: "operations",
                description: "Number of secret provider operations executed.");

            _failureCounter = _meter.CreateCounter<long>(
                name: ComposeMetricName("operation.failures"),
                unit: "failures",
                description: "Number of failed secret provider operations.");

            _durationMs = _meter.CreateHistogram<double>(
                name: ComposeMetricName("operation.duration_ms"),
                unit: "ms",
                description: "Operation duration in milliseconds.");

            _inFlight = _meter.CreateUpDownCounter<long>(
                name: ComposeMetricName("operation.in_flight"),
                unit: "operations",
                description: "Number of concurrent provider operations in flight.");
        }

        public async Task<SecretValue> GetSecretAsync(
            string key,
            SecretQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                "GetSecretAsync",
                key,
                token => _inner.GetSecretAsync(key, query, token),
                cancellationToken);
        }

        public async Task<SecretValue> PutSecretAsync(
            string key,
            string value,
            SecretMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                "PutSecretAsync",
                key,
                token => _inner.PutSecretAsync(key, value, metadata, token),
                cancellationToken);
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(
                "DeleteSecretAsync",
                key,
                token => _inner.DeleteSecretAsync(key, token),
                cancellationToken);
        }

        public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                "GetSecretVersionsAsync",
                key,
                token => _inner.GetSecretVersionsAsync(key, token),
                cancellationToken);
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                "SecretExistsAsync",
                key,
                token => _inner.SecretExistsAsync(key, token),
                cancellationToken);
        }

        private async Task ExecuteAsync(
            string operation,
            string key,
            Func<CancellationToken, Task> operationExecutor,
            CancellationToken cancellationToken)
        {
            await ExecuteAsync<int>(
                operation,
                key,
                async token =>
                {
                    await operationExecutor(token);
                    return 0;
                },
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _inner.DisposeAsync();
        }

        private string ComposeMetricName(string metric)
        {
            return string.IsNullOrEmpty(_metricPrefix)
                ? metric
                : _metricPrefix + metric;
        }

        private async Task<TResult> ExecuteAsync<TResult>(
            string operation,
            string key,
            Func<CancellationToken, Task<TResult>> operationExecutor,
            CancellationToken cancellationToken)
        {
            var inFlight = new TagList
            {
                { "operation", operation }
            };

            _inFlight.Add(1, inFlight);
            _callCounter.Add(1, inFlight);

            var start = Stopwatch.GetTimestamp();

            try
            {
                return await operationExecutor(cancellationToken);
            }
            catch (Exception)
            {
                _failureCounter.Add(1, inFlight);
                throw;
            }
            finally
            {
                var stop = Stopwatch.GetTimestamp();
                _durationMs.Record((stop - start) * TimestampFrequency, inFlight);
                _inFlight.Add(-1, inFlight);
            }
        }
        private static string NormalizeMetricPrefix(string metricPrefix)
        {
            if (string.IsNullOrWhiteSpace(metricPrefix))
                return string.Empty;

            var normalized = metricPrefix.Trim();
            return normalized.EndsWith('.')
                ? normalized
                : normalized + ".";
        }

    }
}
