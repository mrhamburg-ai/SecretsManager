using System.Collections.ObjectModel;

namespace SecretsManager;

/// <summary>
/// Metadata to attach when creating or updating a secret.
/// </summary>
public record SecretMetadata
{
    private readonly IReadOnlyDictionary<string, string>? _tags;

    /// <summary>
    /// MIME type of the secret value (e.g. "application/json", "text/plain").
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Arbitrary key-value tags for categorization and filtering.
    /// The dictionary is copied when assigned, so later changes to the source
    /// dictionary do not alter this instance.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags
    {
        get => _tags;
        init => _tags = value is null
            ? null
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(value));
    }
}
