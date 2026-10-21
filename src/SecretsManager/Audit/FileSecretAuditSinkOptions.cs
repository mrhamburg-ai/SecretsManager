namespace SecretsManager;

/// <summary>
/// Options for <see cref="FileSecretAuditSink"/>.
/// </summary>
public sealed class FileSecretAuditSinkOptions
{
    /// <summary>
    /// Force every record to durable storage before the write completes. Defaults to <c>true</c>:
    /// an unflushed audit record is not an audit record. Set to <c>false</c> to leave flushing to
    /// the operating system.
    /// </summary>
    public bool FlushEachWrite { get; set; } = true;

    /// <summary>
    /// Optional maximum file size in bytes before the file is rotated to <c>&lt;path&gt;.&lt;UTC timestamp&gt;</c>. Rotated files are never
    /// overwritten or deleted.
    /// </summary>
    public long? MaxFileSizeBytes { get; set; }
}
