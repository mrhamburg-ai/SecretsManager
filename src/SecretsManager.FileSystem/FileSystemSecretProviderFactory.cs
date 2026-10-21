using SecretsManager.Builder;

namespace SecretsManager.FileSystem;

/// <summary>
/// Factory that creates a <see cref="FileSystemSecretProvider"/> from a flat configuration dictionary.
/// Used by <see cref="SecretProviderBuilder"/> when the "filesystem" provider is selected.
/// </summary>
public sealed class FileSystemSecretProviderFactory : ISecretProviderFactory
{
    private readonly FileSystemOptions? _capturedOptions;

    /// <summary>
    /// Initializes a factory that builds its options purely from the settings dictionary. Used when
    /// configuration comes from YAML, environment variables, or <c>IConfiguration</c>.
    /// </summary>
    public FileSystemSecretProviderFactory()
    {
    }

    /// <summary>
    /// Initializes a factory that starts from options already configured through the fluent API.
    /// </summary>
    /// <param name="capturedOptions">
    /// The options produced by the <c>WithFileSystem</c> configuration action. They are used as the
    /// base, and the string settings override the values that are expressible as strings.
    /// </param>
    /// <remarks>
    /// This overload exists because <see cref="ISecretProviderFactory.Create"/> accepts only strings.
    /// Without it, an object-valued option such as
    /// <see cref="FileSystemOptions.EncryptionKeyProvider"/> is silently dropped, the provider finds
    /// no key, and it falls back to writing plaintext.
    /// </remarks>
    public FileSystemSecretProviderFactory(FileSystemOptions capturedOptions)
    {
        ArgumentNullException.ThrowIfNull(capturedOptions);
        _capturedOptions = capturedOptions;
    }

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
        // Never mutate the captured instance: Build() may be called more than once on one builder,
        // and each call would otherwise overwrite the previous one's settings.
        var options = _capturedOptions is null ? new FileSystemOptions() : Clone(_capturedOptions);

        if (configuration.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path))
            options.BasePath = path;

        if (configuration.TryGetValue("encryption.key", out var key) && !string.IsNullOrWhiteSpace(key))
            options.EncryptionKey = key;

        if (configuration.TryGetValue("encryption.keyfile", out var keyFile) && !string.IsNullOrWhiteSpace(keyFile))
            options.EncryptionKeyFile = keyFile;

        return new FileSystemSecretProvider(options);
    }

    private static FileSystemOptions Clone(FileSystemOptions source) => new()
    {
        BasePath = source.BasePath,
        EncryptionKey = source.EncryptionKey,
        EncryptionKeyFile = source.EncryptionKeyFile,

        // An object, so it is shared rather than copied: the key provider is a stateless reader and
        // the caller owns its lifetime.
        EncryptionKeyProvider = source.EncryptionKeyProvider
    };
}
