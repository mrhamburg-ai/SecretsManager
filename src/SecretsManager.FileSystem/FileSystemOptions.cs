namespace SecretsManager.FileSystem;

public sealed class FileSystemOptions
{
    public string BasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecretsManager");

    public string? EncryptionKey { get; set; }
    public string? EncryptionKeyFile { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(BasePath))
            yield return "BasePath is required";
        if (!string.IsNullOrEmpty(EncryptionKey) && !string.IsNullOrEmpty(EncryptionKeyFile))
            yield return "EncryptionKey and EncryptionKeyFile are mutually exclusive";
    }
}
