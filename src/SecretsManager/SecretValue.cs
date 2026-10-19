namespace SecretsManager;

/// <summary>
/// Represents a secret retrieved from a backend store.
/// </summary>
public record SecretValue
{
    /// <summary>
    /// The key (name) that identifies the secret.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The secret's value, decoded as a UTF-8 string.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Provider-specific version identifier, when the backend supports versioning.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Timestamp when this version of the secret was created, when reported by the backend.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this version of the secret was last updated, when reported by the backend.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Optional content type of the value (for example <c>application/json</c>).
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Optional tags attached to the secret.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}
