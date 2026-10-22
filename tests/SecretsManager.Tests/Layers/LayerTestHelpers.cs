using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecretsManager.Builder;

namespace SecretsManager.Tests;

internal static class LayerTestHelpers
{
    public static void RecordMax(ref int target, int value)
    {
        while (true)
        {
            var previous = Volatile.Read(ref target);
            if (value <= previous)
                return;

            if (Interlocked.CompareExchange(ref target, value, previous) == previous)
                return;
        }
    }
}

internal sealed class DelegatingSecretProvider : ISecretProvider
{
    public Func<string, SecretQuery?, CancellationToken, Task<SecretValue>> GetSecretAsyncImpl { get; set; } =
        (_, _, _) => Task.FromResult(new SecretValue { Key = string.Empty, Value = string.Empty });

    public Func<string, string, SecretMetadata?, CancellationToken, Task<SecretValue>> PutSecretAsyncImpl { get; set; } =
        (_, value, _, _) => Task.FromResult(new SecretValue { Key = string.Empty, Value = value });

    public Func<string, CancellationToken, Task> DeleteSecretAsyncImpl { get; set; } =
        (_, _) => Task.CompletedTask;

    public Func<string, CancellationToken, Task<IReadOnlyList<SecretVersionInfo>>> GetSecretVersionsAsyncImpl { get; set; } =
        (_, _) => Task.FromResult((IReadOnlyList<SecretVersionInfo>)Array.Empty<SecretVersionInfo>());

    public Func<string, CancellationToken, Task<bool>> SecretExistsAsyncImpl { get; set; } =
        (_, _) => Task.FromResult(true);

    public Task<SecretValue> GetSecretAsync(string key, SecretQuery? query = null, CancellationToken cancellationToken = default) =>
        GetSecretAsyncImpl(key, query, cancellationToken);

    public Task<SecretValue> PutSecretAsync(string key, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default) =>
        PutSecretAsyncImpl(key, value, metadata, cancellationToken);

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default) =>
        DeleteSecretAsyncImpl(key, cancellationToken);

    public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(string key, CancellationToken cancellationToken = default) =>
        GetSecretVersionsAsyncImpl(key, cancellationToken);

    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default) =>
        SecretExistsAsyncImpl(key, cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TestLayerProviderFactory : ISecretProviderFactory
{
    private readonly DelegatingSecretProvider _provider;

    public TestLayerProviderFactory(DelegatingSecretProvider provider)
    {
        _provider = provider;
    }

    public string ProviderName => "test";

    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        return _provider;
    }
}

internal sealed class TestTrackingLayer : ISecretProviderLayer
{
    private readonly string _name;
    private readonly IList<string> _callOrder;

    public TestTrackingLayer(string name, IList<string> callOrder)
    {
        _name = name;
        _callOrder = callOrder;
    }

    public ISecretProvider Wrap(ISecretProvider inner)
        => new TrackingProvider(inner, _name, _callOrder);

    private sealed class TrackingProvider(ISecretProvider inner, string name, IList<string> callOrder) : ISecretProvider
    {
        private readonly ISecretProvider _inner = inner;
        private readonly string _name = name;
        private readonly IList<string> _callOrder = callOrder;

        public async Task<SecretValue> GetSecretAsync(string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"{_name}-enter-get");
            var value = await _inner.GetSecretAsync(key, query, cancellationToken);
            _callOrder.Add($"{_name}-exit-get");
            return value;
        }

        public async Task<SecretValue> PutSecretAsync(string key, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"{_name}-enter-put");
            var result = await _inner.PutSecretAsync(key, value, metadata, cancellationToken);
            _callOrder.Add($"{_name}-exit-put");
            return result;
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"{_name}-enter-delete");
            await _inner.DeleteSecretAsync(key, cancellationToken);
            _callOrder.Add($"{_name}-exit-delete");
        }

        public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(string key, CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"{_name}-enter-versions");
            var versions = await _inner.GetSecretVersionsAsync(key, cancellationToken);
            _callOrder.Add($"{_name}-exit-versions");
            return versions;
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"{_name}-enter-exists");
            var exists = await _inner.SecretExistsAsync(key, cancellationToken);
            _callOrder.Add($"{_name}-exit-exists");
            return exists;
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}

internal sealed class ThrowingSecretProviderLogger : ISecretProviderLogger
{
    public bool ThrowOnStart { get; init; }
    public bool ThrowOnSuccess { get; init; }
    public bool ThrowOnFailure { get; init; }
    public bool ThrowOnRetry { get; init; }
    public List<string> Calls { get; } = new();

    public void LogStart(SecretProviderLogContext context)
    {
        Calls.Add("start");
        if (ThrowOnStart)
            throw new IOException("sink down");
    }

    public void LogSuccess(SecretProviderLogContext context, TimeSpan duration)
    {
        Calls.Add("success");
        if (ThrowOnSuccess)
            throw new IOException("sink down");
    }

    public void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration)
    {
        Calls.Add("failure");
        if (ThrowOnFailure)
            throw new IOException("sink down");
    }
}

internal sealed class RecordingLogger : ISecretProviderLogger
{
    public record Entry(string Type, string Operation, Exception? Exception, TimeSpan? Duration = null);

    public List<Entry> Entries { get; } = new();

    public void LogStart(SecretProviderLogContext context)
    {
        Entries.Add(new Entry("start", context.Operation, null));
    }

    public void LogSuccess(SecretProviderLogContext context, TimeSpan duration)
    {
        Entries.Add(new Entry("success", context.Operation, null, duration));
    }

    public void LogFailure(SecretProviderLogContext context, Exception exception, TimeSpan duration)
    {
        Entries.Add(new Entry("failure", context.Operation, exception, duration));
    }
}
