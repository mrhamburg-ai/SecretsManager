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
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

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

        var bytes = JsonSerializer.SerializeToUtf8Bytes(auditEvent, SerializerOptions);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RotateIfNeeded();

            var streamOptions = new FileStreamOptions
            {
                Mode = FileMode.Append,
                Access = FileAccess.Write,
                Share = FileShare.ReadWrite,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous
            };

            if (!OperatingSystem.IsWindows())
                streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            await using var stream = new FileStream(_path, streamOptions);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);

            if (_options.FlushEachWrite)
                stream.Flush(flushToDisk: true);
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

        // A unique name per rotation: overwriting a previous rotated file would destroy audit
        // records. Keep the original extension so "*.jsonl" globs still find rotated files.
        var directory = Path.GetDirectoryName(_path) ?? string.Empty;
        var rotatedName =
            $"{Path.GetFileNameWithoutExtension(_path)}.{DateTime.UtcNow:yyyyMMddTHHmmssfffffff}{Path.GetExtension(_path)}";
        File.Move(_path, Path.Combine(directory, rotatedName), overwrite: false);
    }
}
