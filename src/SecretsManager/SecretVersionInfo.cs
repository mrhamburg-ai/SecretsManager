namespace SecretsManager;

/// <summary>
/// Describes a single version of a secret without exposing its value.
/// </summary>
public record SecretVersionInfo
{
    /// <summary>
    /// Provider-specific version identifier.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Timestamp when this version was created, when reported by the backend.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Whether this version is the one the backend currently serves.
    /// </summary>
    public bool IsCurrent { get; init; }
}
