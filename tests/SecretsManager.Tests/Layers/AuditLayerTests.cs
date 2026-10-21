using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Tests;

public sealed class AuditLayerTests
{
    private static AuditLayer CreateLayer(
        ISecretAuditSink sink,
        Action<AuditLayerOptions>? configure = null)
    {
        var options = new AuditLayerOptions
        {
            ProviderName = "test"
        };
        options.Sinks.Add(sink);
        configure?.Invoke(options);
        return new AuditLayer(options);
    }

    [Fact]
    public async Task AuditLayer_EmitsOneRecordPerOperation()
    {
        var sink = new RecordingAuditSink();
        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
                Task.FromResult(new SecretValue { Key = key, Value = "value", Version = "1" }),
            PutSecretAsyncImpl = (key, value, _, _) =>
                Task.FromResult(new SecretValue { Key = key, Value = value, Version = "2" }),
            GetSecretVersionsAsyncImpl = (_, _) =>
                Task.FromResult((IReadOnlyList<SecretVersionInfo>)Array.Empty<SecretVersionInfo>())
        };

        var provider = CreateLayer(sink).Wrap(baseProvider);

        await provider.GetSecretAsync("read-key");
        await provider.PutSecretAsync("write-key", "value");
        await provider.DeleteSecretAsync("delete-key");
        await provider.GetSecretVersionsAsync("versions-key");
        await provider.SecretExistsAsync("exists-key");

        var operations = sink.Events
            .Where(e => e.Operation != "DisposeAsync")
            .ToList();

        Assert.Equal(5, operations.Count);
        Assert.All(operations, e => Assert.Equal("test", e.Provider));
        Assert.All(operations, e => Assert.Equal(SecretAuditOutcome.Success, e.Outcome));
        Assert.Contains(operations, e => e.Operation == "GetSecretAsync" && e.Key == "read-key" && e.Version == "1");
        Assert.Contains(operations, e => e.Operation == "PutSecretAsync" && e.Key == "write-key" && e.Version == "2");
        Assert.Contains(operations, e => e.Operation == "DeleteSecretAsync" && e.Key == "delete-key");
        Assert.Contains(operations, e => e.Operation == "GetSecretVersionsAsync" && e.Key == "versions-key");
        Assert.Contains(operations, e => e.Operation == "SecretExistsAsync" && e.Key == "exists-key");
    }

    [Fact]
    public async Task AuditLayer_PopulatesActorFromAmbientAccessor()
    {
        var sink = new RecordingAuditSink();
        var accessor = new AmbientSecretActorAccessor();
        var provider = CreateLayer(sink, o => o.ActorAccessor = accessor)
            .Wrap(new DelegatingSecretProvider());

        using (accessor.Push(new SecretActor("svc:reporting", "Reporting service")))
        {
            await provider.GetSecretAsync("db/password");
        }

        var auditEvent = Assert.Single(sink.Events, e => e.Operation == "GetSecretAsync");
        Assert.Equal("svc:reporting", auditEvent.ActorId);
        Assert.Equal("Reporting service", auditEvent.ActorDisplayName);
    }

    [Fact]
    public async Task AuditLayer_ThrowingSinkDoesNotFailSuccessfulOperation()
    {
        var sink = new ThrowingAuditSink();
        var errors = new List<Exception>();
        var provider = CreateLayer(sink, o => o.OnSinkError = errors.Add)
            .Wrap(new DelegatingSecretProvider
            {
                GetSecretAsyncImpl = (key, _, _) =>
                    Task.FromResult(new SecretValue { Key = key, Value = "value" })
            });

        var result = await provider.GetSecretAsync("key");

        Assert.Equal("value", result.Value);
        Assert.Single(errors);
        Assert.IsType<IOException>(errors[0]);
        Assert.Single(sink.Events);
        Assert.Equal(SecretAuditOutcome.Success, sink.Events[0].Outcome);
    }

    [Fact]
    public async Task AuditLayer_FailedOperationRecordsFailureTypeButNoValue()
    {
        const string secretValue = "super-secret-value";
        var sink = new RecordingAuditSink();
        var provider = CreateLayer(sink).Wrap(new DelegatingSecretProvider
        {
            PutSecretAsyncImpl = (_, _, _, _) =>
                Task.FromException<SecretValue>(
                    new SecretProviderException($"backend rejected {secretValue}"))
        });

        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("key", secretValue));

        var auditEvent = Assert.Single(sink.Events, e => e.Operation == "PutSecretAsync");
        Assert.Equal(SecretAuditOutcome.Failure, auditEvent.Outcome);
        Assert.Equal("SecretsManager.SecretProviderException", auditEvent.FailureType);

        var serialized = JsonSerializer.Serialize(auditEvent);
        Assert.DoesNotContain(secretValue, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditLayer_ThrowingSinkDoesNotMaskOriginalFailure()
    {
        var sink = new ThrowingAuditSink();
        var provider = CreateLayer(sink).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, _) =>
                Task.FromException<SecretValue>(new InvalidOperationException("backend failed"))
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetSecretAsync("key"));

        Assert.Equal("backend failed", ex.Message);
        Assert.Single(sink.Events);
        Assert.Equal(SecretAuditOutcome.Failure, sink.Events[0].Outcome);
    }

    [Fact]
    public async Task AuditLayer_WritesRecordWhenCallerCancels()
    {
        var sink = new RecordingAuditSink();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var provider = CreateLayer(sink).Wrap(new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (_, _, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new SecretValue { Key = "key", Value = "value" });
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetSecretAsync("key", cancellationToken: cts.Token));

        var auditEvent = Assert.Single(sink.Events, e => e.Operation == "GetSecretAsync");
        Assert.Equal(SecretAuditOutcome.Failure, auditEvent.Outcome);
        Assert.Equal("System.OperationCanceledException", auditEvent.FailureType);
    }

    [Fact]
    public async Task AuditLayer_ScopeFiltersOperations()
    {
        var sink = new RecordingAuditSink();
        var provider = CreateLayer(sink, o => o.Scope = SecretAuditScope.Writes)
            .Wrap(new DelegatingSecretProvider());

        await provider.GetSecretAsync("read-key");
        await provider.PutSecretAsync("write-key", "value");

        var auditEvent = Assert.Single(sink.Events, e => e.Operation != "DisposeAsync");
        Assert.Equal("PutSecretAsync", auditEvent.Operation);
    }

    [Fact]
    public async Task AuditLayer_EmitsDisposeRecord()
    {
        var sink = new RecordingAuditSink();
        var provider = CreateLayer(sink).Wrap(new DelegatingSecretProvider());

        await provider.DisposeAsync();

        var auditEvent = Assert.Single(sink.Events);
        Assert.Equal("DisposeAsync", auditEvent.Operation);
        Assert.Equal(SecretAuditOutcome.Success, auditEvent.Outcome);
    }

    [Fact]
    public void AuditLayer_WithoutSinks_ThrowsAtWrap()
    {
        var layer = new AuditLayer(new AuditLayerOptions { ProviderName = "test" });

        var ex = Assert.Throws<ArgumentException>(
            () => layer.Wrap(new DelegatingSecretProvider()));

        Assert.Contains("At least one audit sink is required", ex.Message);
    }

    [Fact]
    public async Task Builder_SetsProviderNameOnAuditLayer()
    {
        var sink = new RecordingAuditSink();
        var baseProvider = new DelegatingSecretProvider
        {
            GetSecretAsyncImpl = (key, _, _) =>
                Task.FromResult(new SecretValue { Key = key, Value = "value" })
        };

        var builder = new SecretProviderBuilder()
            .RegisterProvider(new TestLayerProviderFactory(baseProvider))
            .UseProvider("test")
            .WithAudit(o => o.Sinks.Add(sink));

        await using var provider = builder.Build();
        await provider.GetSecretAsync("key");

        var auditEvent = Assert.Single(sink.Events, e => e.Operation == "GetSecretAsync");
        Assert.Equal("test", auditEvent.Provider);
    }

    [Fact]
    public async Task WithAuditSink_FansOutToMultipleSinks()
    {
        var first = new RecordingAuditSink();
        var second = new RecordingAuditSink();
        var baseProvider = new DelegatingSecretProvider();

        var builder = new SecretProviderBuilder()
            .RegisterProvider(new TestLayerProviderFactory(baseProvider))
            .UseProvider("test")
            .WithAuditSink(first)
            .WithAuditSink(second);

        await using var provider = builder.Build();
        await provider.GetSecretAsync("key");

        Assert.Contains(first.Events, e => e.Operation == "GetSecretAsync");
        Assert.Contains(second.Events, e => e.Operation == "GetSecretAsync");
    }

    [Fact]
    public void ConfigureAudit_Disabled_DoesNotAddLayer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Audit:Enabled"] = "false"
            })
            .Build();

        var builder = new SecretProviderBuilder().ConfigureAudit(configuration);

        Assert.NotNull(builder);
    }

    [Fact]
    public void ConfigureAudit_InvalidScope_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Audit:Enabled"] = "true",
                ["SecretsManager:Audit:Scope"] = "Everything"
            })
            .Build();

        var ex = Assert.Throws<ArgumentException>(
            () => { _ = new SecretProviderBuilder().ConfigureAudit(configuration); });

        Assert.Contains("Scope must be", ex.Message);
    }
}

internal sealed class RecordingAuditSink : ISecretAuditSink
{
    private readonly List<SecretAuditEvent> _events = new();

    public IReadOnlyList<SecretAuditEvent> Events
    {
        get
        {
            lock (_events)
                return _events.ToArray();
        }
    }

    public ValueTask WriteAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        lock (_events)
            _events.Add(auditEvent);

        return ValueTask.CompletedTask;
    }
}

internal sealed class ThrowingAuditSink : ISecretAuditSink
{
    private readonly List<SecretAuditEvent> _events = new();

    public IReadOnlyList<SecretAuditEvent> Events
    {
        get
        {
            lock (_events)
                return _events.ToArray();
        }
    }

    public ValueTask WriteAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        lock (_events)
            _events.Add(auditEvent);

        throw new IOException("sink is down");
    }
}
