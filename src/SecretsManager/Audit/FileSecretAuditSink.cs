using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace SecretsManager;

/// <summary>
/// Appends audit records to a file as JSON Lines.
/// </summary>
public sealed class FileSecretAuditSink : ISecretAuditSink
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks =
        new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _path;
    private readonly FileSecretAuditSinkOptions _options;
    private readonly SemaphoreSlim _lock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSecretAuditSink"/> class.
    /// </summary>
    /// <param name="path">File path. The parent directory is created when it does not exist.</param>
    /// <param name="options">Optional sink options.</param>
    /// <exception cref="ArgumentException">The path is empty or the options are invalid.</exception>
    public FileSecretAuditSink(string path, FileSecretAuditSinkOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        _options = options ?? new FileSecretAuditSinkOptions();
        if (_options.MaxFileSizeBytes is <= 0)
            throw new ArgumentException("MaxFileSizeBytes must be greater than zero.", nameof(options));

        _path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _lock = FileLocks.GetOrAdd(_path, static _ => new SemaphoreSlim(1, 1));
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        SecretAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var line = JsonSerializer.Serialize(auditEvent, SerializerOptions) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(line);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RotateIfNeeded();

            await using var stream = new FileStream(
                _path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                FileOptions.Asynchronous);

            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

            if (_options.FlushEachWrite)
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void RotateIfNeeded()
    {
        if (_options.MaxFileSizeBytes is not { } maxFileSize)
            return;

        var file = new FileInfo(_path);
        if (!file.Exists || file.Length < maxFileSize)
            return;

        File.Move(_path, _path + ".1", overwrite: true);
    }
}
