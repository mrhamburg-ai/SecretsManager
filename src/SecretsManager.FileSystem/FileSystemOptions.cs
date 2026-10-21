using SecretsManager.Internal;

namespace SecretsManager.FileSystem;

/// <summary>
/// Configuration options for the filesystem secret provider.
/// </summary>
public sealed class FileSystemOptions
{
    /// <summary>
    /// Root directory where secrets are stored. Each secret gets a subdirectory.
    /// Defaults to {LocalApplicationData}/SecretsManager.
    /// </summary>
    public string BasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecretsManager");

    /// <summary>
    /// Base64-encoded AES-256 key (32 bytes). When set, secrets are encrypted at rest.
    /// Mutually exclusive with <see cref="EncryptionKeyFile"/>.
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Path to a file containing the raw or base64-encoded AES-256 key.
    /// Mutually exclusive with <see cref="EncryptionKey"/>.
    /// </summary>
    public string? EncryptionKeyFile { get; set; }

    /// <summary>
    /// Supplies the encryption key from an external source, for example another secret provider.
    /// Mutually exclusive with <see cref="EncryptionKey"/> and <see cref="EncryptionKeyFile"/> for
    /// new writes. Because this is an object it cannot come from appsettings.json, YAML or
    /// environment variables — set it through the fluent API.
    /// </summary>
    public IEncryptionKeyProvider? EncryptionKeyProvider { get; set; }

    /// <summary>
    /// Validates the options and returns error messages.
    /// </summary>
    /// <returns>Validation error messages. An empty collection indicates the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(BasePath))
            yield return "BasePath is required";
        if (!string.IsNullOrEmpty(EncryptionKey) && !string.IsNullOrEmpty(EncryptionKeyFile))
            yield return "EncryptionKey and EncryptionKeyFile are mutually exclusive";

        var writeSources = new[]
        {
            EncryptionKey is not null,
            EncryptionKeyFile is not null,
            EncryptionKeyProvider is not null
        };
        if (writeSources.Count(x => x) > 1)
            yield return "EncryptionKey, EncryptionKeyFile and EncryptionKeyProvider are mutually exclusive";
    }
}
