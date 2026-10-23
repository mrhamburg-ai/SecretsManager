using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecretsManager.Tests;

public sealed class TimeoutLayerTests
{
    [Fact]
    public async Task TimeoutLayer_ThrowsTimeoutException()
    {
        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = async (_, _, token) =>
            {
                // Long enough that the timeout always fires first, even on a loaded CI runner.
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return new SecretValue { Key = "key", Value = "slow" };
            }
        };

        await using var provider = new TimeoutLayer(new TimeoutLayerOptions
        {
            Timeout = TimeSpan.FromMilliseconds(20)
        }).Wrap(baseProvider);

        await Assert.ThrowsAsync<TimeoutException>(() => provider.GetSecretAsync("key"));
    }

    [Fact]
    public async Task TimeoutLayer_RespectsCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = async (_, _, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new SecretValue { Key = "key", Value = "slow" };
            }
        };

        await using var provider = new TimeoutLayer(new TimeoutLayerOptions
        {
            Timeout = TimeSpan.FromMilliseconds(20)
        }).Wrap(baseProvider);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetSecretAsync("key", cancellationToken: cts.Token));
    }
}
