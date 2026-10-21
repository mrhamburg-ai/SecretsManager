namespace SecretsManager;

/// <summary>
/// A single, immutable audit record describing one attempted secret operation.
/// The record never contains a secret value.
/// </summary>
public sealed record SecretAuditEvent
{
    /// <summary>The operation that was attempted, for example <c>GetSecretAsync</c>.</summary>
    public required string Operation { get; init; }

    /// <summary>The registered provider name, for example <c>vault</c> or <c>awsSecretsManager</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The secret key the operation targeted, when it targets one secret.</summary>
    public string? Key { get; init; }

    /// <summary>The resolved version, when the operation returned one.</summary>
    public string? Version { get; init; }

    /// <summary>Whether the operation succeeded, failed, was cancelled, or was denied.</summary>
    public required SecretAuditOutcome Outcome { get; init; }

    /// <summary>When the operation started, captured by the library.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>How long the operation took.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Who performed the operation. Null when no actor is in scope.</summary>
    public string? ActorId { get; init; }

    /// <summary>Optional human-readable actor name.</summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>Ambient request or correlation id, when the host set one.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The distributed trace id, so audit records can be joined to traces.</summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// The exception type when the operation failed. The exception message is deliberately not
    /// recorded because backend messages can contain secret values.
    /// </summary>
    public string? FailureType { get; init; }
}
