using System;
using System.Linq;
using System.Threading.Tasks;
using SecretsManager.Builder;

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

    [Fact]
    public async Task RetryLayer_LogsEachRetryWithAttemptAndDelay()
    {
        var logger = new RecordingLogger();
        var attempts = 0;

        var provider = new RetryLayer(new RetryLayerOptions
        {
            Logger = logger,
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1),
            JitterFactor = 0
        }).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
            {
                attempts++;
                if (attempts < 3)
                    throw new TimeoutException("temporary");

                return Task.FromResult(new SecretValue { Key = key, Value = "ok" });
            }
        });

        await provider.GetSecretAsync("key");

        var retries = logger.Entries.Where(e => e.Type == "retry").ToList();
        Assert.Equal(2, retries.Count);
        Assert.Equal(2, retries[0].Attempt);
        Assert.Equal(3, retries[1].Attempt);
        Assert.Equal(TimeSpan.FromMilliseconds(1), retries[0].Delay);
        Assert.Equal("GetSecretAsync", retries[0].Context.Operation);
        Assert.Equal("key", retries[0].Context.Key);
    }

    [Fact]
    public async Task RetryLayer_ThrowingRetryLoggerStillRetries()
    {
        var logger = new ThrowingSecretProviderLogger { ThrowOnRetry = true };
        var attempts = 0;

        var provider = new RetryLayer(new RetryLayerOptions
        {
            Logger = logger,
            MaxAttempts = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1),
            JitterFactor = 0
        }).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
            {
                attempts++;
                if (attempts == 1)
                    throw new TimeoutException("temporary");

                return Task.FromResult(new SecretValue { Key = key, Value = "ok" });
            }
        });

        var result = await provider.GetSecretAsync("key");

        Assert.Equal("ok", result.Value);
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Builder_SharesLoggerWithRetryLayer(bool loggingFirst)
    {
        var logger = new RecordingLogger();
        var attempts = 0;
        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
            {
                attempts++;
                if (attempts == 1)
                    throw new TimeoutException("temporary");

                return Task.FromResult(new SecretValue { Key = key, Value = "ok" });
            }
        };

        var builder = new SecretProviderBuilder()
            .RegisterProvider(new TestLayerProviderFactory(baseProvider))
            .UseProvider("test");

        if (loggingFirst)
            builder.WithLogging(o => o.Logger = logger).WithRetry(o =>
            {
                o.MaxAttempts = 2;
                o.BaseDelay = TimeSpan.FromMilliseconds(1);
                o.MaxDelay = TimeSpan.FromMilliseconds(1);
                o.JitterFactor = 0;
            });
        else
            builder.WithRetry(o =>
            {
                o.MaxAttempts = 2;
                o.BaseDelay = TimeSpan.FromMilliseconds(1);
                o.MaxDelay = TimeSpan.FromMilliseconds(1);
                o.JitterFactor = 0;
            }).WithLogging(o => o.Logger = logger);

        await using var provider = builder.Build();
        await provider.GetSecretAsync("key");

        Assert.Contains(logger.Entries, e => e.Type == "retry" && e.Context.Provider == "test");
    }

    [Fact]
    public async Task RetryLayer_UsesExponentialBackoffStartingAtBaseDelay()
    {
        var logger = new RecordingLogger();
        var attempts = 0;

        var provider = new RetryLayer(new RetryLayerOptions
        {
            Logger = logger,
            MaxAttempts = 4,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(1_000),
            JitterFactor = 0
        }).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
            {
                attempts++;
                throw new TimeoutException("temporary");
            }
        });

        await Assert.ThrowsAsync<TimeoutException>(() => provider.GetSecretAsync("key"));

        Assert.Equal(4, attempts);
        var delays = logger.Entries
            .Where(e => e.Type == "retry")
            .Select(e => e.Delay!.Value)
            .ToArray();
        Assert.Equal(
            [
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(20),
                TimeSpan.FromMilliseconds(40)
            ],
            delays);
    }

    [Fact]
    public async Task RetryLayer_ClampsJitteredDelayToMaxDelay()
    {
        var logger = new RecordingLogger();

        var provider = new RetryLayer(new RetryLayerOptions
        {
            Logger = logger,
            MaxAttempts = 6,
            BaseDelay = TimeSpan.FromMilliseconds(20),
            MaxDelay = TimeSpan.FromMilliseconds(25),
            JitterFactor = 1
        }).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) => throw new TimeoutException("temporary")
        });

        await Assert.ThrowsAsync<TimeoutException>(() => provider.GetSecretAsync("key"));

        var delays = logger.Entries.Where(e => e.Type == "retry").Select(e => e.Delay!.Value).ToList();
        Assert.Equal(5, delays.Count);
        Assert.All(delays, delay => Assert.InRange(
            delay, TimeSpan.Zero, TimeSpan.FromMilliseconds(25)));
    }
}
