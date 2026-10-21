using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SecretsManager.PostgreSql.Internal;

namespace SecretsManager.PostgreSql.Tests;

public sealed class PostgreSqlSecretAuditSinkTests
{
    [Fact]
    public async Task WriteAsync_ForwardsEventToStoreWithoutModification()
    {
        var store = Substitute.For<IPostgreSqlAuditStore>();
        var sink = new PostgreSqlSecretAuditSink(store);
        var auditEvent = new SecretAuditEvent
        {
            Operation = "GetSecretAsync",
            Provider = "postgresql",
            Key = "db/password",
            Version = "3",
            Outcome = SecretAuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(12),
            ActorId = "svc:reporting",
            ActorDisplayName = "Reporting",
            CorrelationId = "corr-1",
            TraceId = "trace-1"
        };

        await sink.WriteAsync(auditEvent);

        await store.Received(1).WriteAsync(auditEvent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_InvalidOptions_Throws()
    {
        var options = new PostgreSqlAuditSinkOptions();

        var ex = Assert.Throws<ArgumentException>(
            () => new PostgreSqlSecretAuditSink(options));

        Assert.Contains("ConnectionString is required", ex.Message);
    }
}
