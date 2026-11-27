using System.Collections.ObjectModel;
using System.Text;

namespace SecretsManager;

/// <summary>
/// Represents a secret retrieved from a backend store.
/// </summary>
public record SecretValue
{
    private readonly IReadOnlyDictionary<string, string>? _tags;

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
    /// Optional tags attached to the secret. The dictionary is copied when assigned,
    /// so later changes to the source dictionary do not alter this instance.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags
    {
        get => _tags;
        init => _tags = value is null
            ? null
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(value));
    }

    /// <summary>
    /// Writes the members used by the generated <c>ToString()</c>, with <see cref="Value"/> redacted
    /// so that logging or string-formatting a <see cref="SecretValue"/> cannot leak the secret.
    /// </summary>
    /// <param name="builder">The builder that receives the member list.</param>
    /// <returns><see langword="true"/> because at least one member is always written.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Append("Key = ").Append(Key)
            .Append(", Value = [redacted]")
            .Append(", Version = ").Append(Version)
            .Append(", CreatedAt = ").Append(CreatedAt)
            .Append(", UpdatedAt = ").Append(UpdatedAt)
            .Append(", ContentType = ").Append(ContentType);
        return true;
    }
}
