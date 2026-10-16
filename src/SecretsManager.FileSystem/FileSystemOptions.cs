namespace SecretsManager.FileSystem;

public sealed class FileSystemOptions
{
    /// <summary>
    /// Root directory where secrets are stored. Each secret gets a subdirectory
    /// </summary>
    public string BasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecretsManager");

    /// <summary>
    /// Base64-encoded AES-256 encryption key (32 bytes)
    /// </summary>
    public string? EncryptionKey { get; set; }
    /// <summary>
    /// Path to a file containing the encryption key
    /// </summary>
    public string? EncryptionKeyFile { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(BasePath))
            yield return "BasePath is required";
        if (!string.IsNullOrEmpty(EncryptionKey) && !string.IsNullOrEmpty(EncryptionKeyFile))
            yield return "EncryptionKey and EncryptionKeyFile are mutually exclusive";
    }
}
