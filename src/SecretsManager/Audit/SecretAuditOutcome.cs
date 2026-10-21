using System.Text.Json.Serialization;

namespace SecretsManager;

/// <summary>
/// The result of an audited secret operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SecretAuditOutcome>))]
public enum SecretAuditOutcome
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The operation failed with an exception.</summary>
    Failure,

    /// <summary>The caller canceled the operation.</summary>
    Cancelled,

    /// <summary>The operation was denied before it reached the provider.</summary>
    Denied
}
