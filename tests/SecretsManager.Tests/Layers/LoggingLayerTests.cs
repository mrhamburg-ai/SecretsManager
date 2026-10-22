using System;
using System.Threading.Tasks;

namespace SecretsManager.Tests;

public sealed class LoggingLayerTests
{
    [Fact]
    public async Task LoggingLayer_ForwardsLifecycleEvents()
    {
        var logger = new RecordingLogger();

        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
            {
                if (key == "explode")
                    return Task.FromException<SecretValue>(new InvalidOperationException("boom"));

                return Task.FromResult(new SecretValue { Key = "key", Value = "ok" });
            },
            PutSecretAsyncImpl = (key, value, _, _) => Task.FromResult(new SecretValue { Key = key, Value = value })
        };

        await using var provider = new LoggingLayer(new LoggingLayerOptions
        {
            Logger = logger,
            OperationPrefix = "Test"
        }).Wrap(baseProvider);

        await provider.GetSecretAsync("key");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetSecretAsync("explode"));

        Assert.Equal(2, logger.Entries.Count(e => e.Type == "start"));
        Assert.Contains(logger.Entries, e => e.Type == "success" && e.Operation == "Test.GetSecretAsync");
        Assert.Contains(logger.Entries, e => e.Type == "failure" && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task LoggingLayer_ThrowingStartLogger_StillReturnsSecret()
    {
        var provider = new LoggingLayer(new LoggingLayerOptions
        {
            Logger = new ThrowingSecretProviderLogger { ThrowOnStart = true }
        }).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
                Task.FromResult(new SecretValue { Key = key, Value = "ok" })
        });

        var result = await provider.GetSecretAsync("key");

        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task LoggingLayer_ThrowingSuccessLogger_DoesNotReportFailure()
    {
        var logger = new ThrowingSecretProviderLogger { ThrowOnSuccess = true };
        var provider = new LoggingLayer(new LoggingLayerOptions { Logger = logger })
            .Wrap(new DelegatingSecretProvider
            {
                GetSecretAsyncImpl = (key, _, _) =>
                    Task.FromResult(new SecretValue { Key = key, Value = "ok" })
            });

        var result = await provider.GetSecretAsync("key");

        Assert.Equal("ok", result.Value);
        Assert.DoesNotContain("failure", logger.Calls);
    }

    [Fact]
    public async Task LoggingLayer_ThrowingFailureLogger_PreservesOriginalException()
    {
        var provider = new LoggingLayer(new LoggingLayerOptions
        {
            Logger = new ThrowingSecretProviderLogger { ThrowOnFailure = true }
        }).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
                Task.FromException<SecretValue>(new InvalidOperationException("backend failed"))
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetSecretAsync("key"));

        Assert.Equal("backend failed", ex.Message);
    }
}
