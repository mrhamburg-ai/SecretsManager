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
    /// Validates the options and returns error messages.
    /// </summary>
    /// <returns>Validation error messages. An empty collection indicates the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(BasePath))
            yield return "BasePath is required";
        if (!string.IsNullOrEmpty(EncryptionKey) && !string.IsNullOrEmpty(EncryptionKeyFile))
            yield return "EncryptionKey and EncryptionKeyFile are mutually exclusive";
    }
}
