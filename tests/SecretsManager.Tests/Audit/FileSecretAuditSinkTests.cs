using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SecretsManager.Tests;

public sealed class FileSecretAuditSinkTests
{
    [Fact]
    public async Task FileSecretAuditSink_WritesJsonLines()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "audit.jsonl");

        try
        {
            var sink = new FileSecretAuditSink(path);
            await sink.WriteAsync(CreateEvent("GetSecretAsync", "first"));
            await sink.WriteAsync(CreateEvent("PutSecretAsync", "second"));

            var lines = await File.ReadAllLinesAsync(path);
            Assert.Equal(2, lines.Length);

            // The raw line must be readable without the C# type; enums stay as names, not ordinals.
            Assert.Contains("\"outcome\":\"Success\"", lines[0], StringComparison.Ordinal);
            Assert.DoesNotContain("\"outcome\":0", lines[0], StringComparison.Ordinal);

            var first = JsonSerializer.Deserialize<SecretAuditEvent>(
                lines[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(first);
            Assert.Equal("GetSecretAsync", first.Operation);
            Assert.Equal("test", first.Provider);
            Assert.Equal(SecretAuditOutcome.Success, first.Outcome);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileSecretAuditSink_ConcurrentWritesProduceOneLinePerRecord()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "audit.jsonl");

        try
        {
            var sink = new FileSecretAuditSink(path);
            var writes = Enumerable.Range(0, 50)
                .Select(i => sink.WriteAsync(CreateEvent("GetSecretAsync", $"key-{i}")).AsTask());

            await Task.WhenAll(writes);

            var lines = await File.ReadAllLinesAsync(path);
            Assert.Equal(50, lines.Length);
            Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileSecretAuditSink_RotatesWhenMaximumSizeIsReached()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "audit.jsonl");

        try
        {
            var sink = new FileSecretAuditSink(path, new FileSecretAuditSinkOptions
            {
                MaxFileSizeBytes = 1
            });

            await sink.WriteAsync(CreateEvent("GetSecretAsync", "first"));
            await sink.WriteAsync(CreateEvent("GetSecretAsync", "second"));
            await sink.WriteAsync(CreateEvent("GetSecretAsync", "third"));

            // Every record must survive: rotation never overwrites an earlier rotated file.
            var files = Directory.GetFiles(directory, "audit*.jsonl");
            Assert.Equal(3, files.Length);
            Assert.All(files, file => Assert.EndsWith(".jsonl", file, StringComparison.Ordinal));

            var contents = string.Concat(files.Select(File.ReadAllText));
            Assert.Contains("first", contents, StringComparison.Ordinal);
            Assert.Contains("second", contents, StringComparison.Ordinal);
            Assert.Contains("third", contents, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileSecretAuditSink_RejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => new FileSecretAuditSink(""));
    }

    private static SecretAuditEvent CreateEvent(string operation, string key) => new()
    {
        Operation = operation,
        Provider = "test",
        Key = key,
        Outcome = SecretAuditOutcome.Success,
        Timestamp = DateTimeOffset.UtcNow
    };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "sm-audit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
