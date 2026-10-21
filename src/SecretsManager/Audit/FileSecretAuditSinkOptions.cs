namespace SecretsManager;

/// <summary>
/// Options for <see cref="FileSecretAuditSink"/>.
/// </summary>
public sealed class FileSecretAuditSinkOptions
{
    /// <summary>
    /// Flush to disk after every record. Defaults to <c>true</c>: an unflushed audit record is not
    /// an audit record.
    /// </summary>
    public bool FlushEachWrite { get; set; } = true;

    /// <summary>
    /// Optional maximum file size in bytes before the file is rotated to <c>&lt;path&gt;.1</c>.
    /// </summary>
    public long? MaxFileSizeBytes { get; set; }
}
