using System;
using Microsoft.Extensions.Logging;

namespace SecretsManager.Tests;

public sealed class LoggerSecretProviderLoggerTests
{
    [Fact]
    public void IsEnabled_MapsSecretProviderLevels()
    {
        var mel = new CapturingLogger { MinimumLevel = LogLevel.Warning };
        var adapter = new LoggerSecretProviderLogger(mel);

        Assert.False(adapter.IsEnabled(SecretProviderLogLevel.Debug));
        Assert.False(adapter.IsEnabled(SecretProviderLogLevel.Information));
        Assert.True(adapter.IsEnabled(SecretProviderLogLevel.Warning));
        Assert.True(adapter.IsEnabled(SecretProviderLogLevel.Error));
    }

    [Fact]
    public void Logs_UseExpectedLevelsAndFields()
    {
        var mel = new CapturingLogger();
        var adapter = new LoggerSecretProviderLogger(mel);
        var context = new SecretProviderLogContext
        {
            Operation = "GetSecretAsync",
            Key = "db/password",
            Provider = "vault",
            Timestamp = DateTimeOffset.UtcNow
        };
        var failure = new InvalidOperationException("backend failed");

        adapter.LogStart(context);
        adapter.LogSuccess(
            WithOutcome(context, SecretProviderLogOutcome.Success),
            TimeSpan.FromMilliseconds(12));
        adapter.LogFailure(
            WithOutcome(context, SecretProviderLogOutcome.Failure),
            failure,
            TimeSpan.FromMilliseconds(20));
        adapter.LogRetry(context, attempt: 2, delay: TimeSpan.FromMilliseconds(50));

        Assert.Equal(4, mel.Entries.Count);
        Assert.Equal(LogLevel.Debug, mel.Entries[0].Level);
        Assert.Equal(LogLevel.Debug, mel.Entries[1].Level);
        Assert.Equal(LogLevel.Error, mel.Entries[2].Level);
        Assert.Equal(LogLevel.Warning, mel.Entries[3].Level);
        Assert.Same(failure, mel.Entries[2].Exception);
        Assert.Contains("vault", mel.Entries[0].Message);
        Assert.Contains("db/password", mel.Entries[0].Message);
        Assert.Contains("2", mel.Entries[3].Message);
    }

    [Fact]
    public void Constructor_RejectsNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggerSecretProviderLogger(null!));
    }

    private static SecretProviderLogContext WithOutcome(
        SecretProviderLogContext context,
        SecretProviderLogOutcome outcome)
    {
        return new SecretProviderLogContext
        {
            Operation = context.Operation,
            Key = context.Key,
            IsMetadataWrite = context.IsMetadataWrite,
            Provider = context.Provider,
            Timestamp = context.Timestamp,
            Outcome = outcome
        };
    }
}

internal sealed class CapturingLogger : ILogger
{
    public record Entry(LogLevel Level, string Message, Exception? Exception);

    public LogLevel MinimumLevel { get; init; } = LogLevel.Trace;
    public List<Entry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
    }
}
