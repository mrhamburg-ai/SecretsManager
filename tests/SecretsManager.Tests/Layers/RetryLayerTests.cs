using System;
using System.Threading.Tasks;

namespace SecretsManager.Tests;

public sealed class RetryLayerTests
{
    [Fact]
    public async Task RetryLayer_RetriesUntilSuccess()
    {
        var attempts = 0;

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
            {
                attempts++;
                if (attempts < 3)
                    throw new TimeoutException("temporary");

                return Task.FromResult(new SecretValue { Key = "key", Value = "ok" });
            }
        };

        await using var provider = new RetryLayer(new RetryLayerOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1),
            JitterFactor = 0
        }).Wrap(baseProvider);

        var value = await provider.GetSecretAsync("key");

        Assert.Equal("ok", value.Value);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RetryLayer_DoesNotRetryNonTransientFailures()
    {
        var attempts = 0;

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
            {
                attempts++;
                throw new ArgumentException("invalid key");
            }
        };

        await using var provider = new RetryLayer(new RetryLayerOptions
        {
            MaxAttempts = 5,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1)
        }).Wrap(baseProvider);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetSecretAsync("key"));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetryLayer_DoesNotRetryPuts()
    {
        var attempts = 0;

        var baseProvider = new DelegatingSecretProvider
        {
            PutSecretAsyncImpl = (_, _, _, _) =>
            {
                attempts++;
                throw new TimeoutException("temporary");
            }
        };

        await using var provider = new RetryLayer(new RetryLayerOptions
        {
            MaxAttempts = 5,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1)
        }).Wrap(baseProvider);

        await Assert.ThrowsAsync<TimeoutException>(
            () => provider.PutSecretAsync("key", "value"));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetryLayer_DoesNotRetrySecretNotFoundByDefault()
    {
        var attempts = 0;

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
            {
                attempts++;
                return Task.FromException<SecretValue>(new SecretNotFoundException("missing"));
            }
        };

        await using var provider = new RetryLayer(new RetryLayerOptions
        {
            MaxAttempts = 5,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1)
        }).Wrap(baseProvider);

        await Assert.ThrowsAsync<SecretNotFoundException>(() => provider.GetSecretAsync("missing"));

        Assert.Equal(1, attempts);
    }
}
