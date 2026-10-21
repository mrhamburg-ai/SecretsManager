namespace SecretsManager;

/// <summary>
/// Selects which operations produce audit records.
/// </summary>
[Flags]
public enum SecretAuditScope
{
    /// <summary>No operations are audited.</summary>
    None = 0,

    /// <summary>Reads are audited, for example <c>GetSecretAsync</c>.</summary>
    Reads = 1,

    /// <summary>Writes are audited, for example <c>PutSecretAsync</c>.</summary>
    Writes = 2,

    /// <summary>Deletes are audited.</summary>
    Deletes = 4,

    /// <summary>
    /// Name-revealing operations are audited, for example <c>GetSecretVersionsAsync</c>
    /// and <c>SecretExistsAsync</c>.
    /// </summary>
    Enumeration = 8,

    /// <summary>All operations are audited, including provider disposal.</summary>
    All = Reads | Writes | Deletes | Enumeration
}
