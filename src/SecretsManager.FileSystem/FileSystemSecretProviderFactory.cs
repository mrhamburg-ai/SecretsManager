using SecretsManager.Builder;

namespace SecretsManager.FileSystem;

/// <summary>
/// Factory that creates a <see cref="FileSystemSecretProvider"/> from a flat configuration dictionary.
/// Used by <see cref="SecretProviderBuilder"/> when the "filesystem" provider is selected.
/// </summary>
public sealed class FileSystemSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// Gets the provider name used in YAML and environment configuration.
    /// </summary>
    public string ProviderName => "filesystem";

    /// <summary>
    /// Creates a <see cref="FileSystemSecretProvider"/> from a flat configuration dictionary.
    /// </summary>
    /// <param name="configuration">The configuration dictionary containing the keys <c>path</c>, <c>encryption.key</c>, and <c>encryption.keyfile</c>.</param>
    /// <returns>The filesystem secret provider.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new FileSystemOptions();

        if (configuration.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path))
            options.BasePath = path;

        if (configuration.TryGetValue("encryption.key", out var key) && !string.IsNullOrWhiteSpace(key))
            options.EncryptionKey = key;

        if (configuration.TryGetValue("encryption.keyfile", out var keyFile) && !string.IsNullOrWhiteSpace(keyFile))
            options.EncryptionKeyFile = keyFile;

        return new FileSystemSecretProvider(options);
    }
}
