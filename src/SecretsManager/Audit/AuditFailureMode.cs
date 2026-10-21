namespace SecretsManager;

/// <summary>
/// Controls what happens when an audit sink throws.
/// </summary>
public enum AuditFailureMode
{
    /// <summary>
    /// Sink failures are reported through <see cref="AuditLayerOptions.OnSinkError"/> and the
    /// secret operation continues. This is the default and the safe choice for production.
    /// </summary>
    Ignore,

    /// <summary>
    /// Sink failures propagate to the caller after every registered sink has attempted the write.
    /// Use only when the audit trail is a hard requirement and an unwritten record must fail the
    /// secret operation.
    /// </summary>
    Throw
}
