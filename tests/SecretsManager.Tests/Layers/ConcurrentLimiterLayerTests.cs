using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SecretsManager.Tests;

public sealed class ConcurrentLimiterLayerTests
{
    [Fact]
    public async Task ConcurrentLimiterLayer_LimitsConcurrency()
    {
        var active = 0;
        var maxActive = 0;

        var gate = new SemaphoreSlim(0);
        var entered = new SemaphoreSlim(0);

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = async (_, _, token) =>
            {
                var current = Interlocked.Increment(ref active);
                LayerTestHelpers.RecordMax(ref maxActive, current);
                entered.Release();
                await gate.WaitAsync(token);
                Interlocked.Decrement(ref active);
                return new SecretValue { Key = "key", Value = "ok" };
            }
        };

        await using var provider = new ConcurrentLimiterLayer(new ConcurrentLimiterLayerOptions
        {
            MaxConcurrentRequests = 1
        }).Wrap(baseProvider);

        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
            tasks.Add(provider.GetSecretAsync($"key-{i}"));

        for (var i = 0; i < 5; i++)
        {
            await entered.WaitAsync();
            Assert.Equal(1, Volatile.Read(ref maxActive));
            gate.Release();
        }

        await Task.WhenAll(tasks);
        Assert.Equal(1, maxActive);
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task ConcurrentLimiterLayer_ReleasesSlotWhenOperationThrows()
    {
        var calls = 0;

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
            {
                calls++;
                return calls == 1
                    ? Task.FromException<SecretValue>(new InvalidOperationException("boom"))
                    : Task.FromResult(new SecretValue { Key = "key", Value = "ok" });
            }
        };

        await using var provider = new ConcurrentLimiterLayer(new ConcurrentLimiterLayerOptions
        {
            MaxConcurrentRequests = 1
        }).Wrap(baseProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetSecretAsync("first"));
        var result = await provider.GetSecretAsync("second");

        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task ConcurrentLimiterLayer_EnforcesAcquireTimeout()
    {
        var started = new TaskCompletionSource();
        var blocker = new TaskCompletionSource();

        var provider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = async (_, _, token) =>
            {
                started.TrySetResult();
                await blocker.Task.WaitAsync(token);
                return new SecretValue { Key = "k", Value = "v" };
            }
        };

        await using var limited = new ConcurrentLimiterLayer(new ConcurrentLimiterLayerOptions
        {
            MaxConcurrentRequests = 1,
            AcquireTimeout = TimeSpan.FromMilliseconds(20)
        }).Wrap(provider);

        var first = limited.GetSecretAsync("one");
        await started.Task;

        await Assert.ThrowsAsync<TimeoutException>(() => limited.GetSecretAsync("two"));

        blocker.SetResult();
        await first;
    }
}
