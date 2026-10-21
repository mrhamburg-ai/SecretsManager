namespace SecretsManager;

/// <summary>
/// The result of an audited secret operation.
/// </summary>
public enum SecretAuditOutcome
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The operation failed with an exception.</summary>
    Failure,

    /// <summary>The operation was denied before it reached the provider.</summary>
    Denied
}
