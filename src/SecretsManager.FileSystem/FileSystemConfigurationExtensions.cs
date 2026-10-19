using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.FileSystem;

/// <summary>
/// Configuration bridge for the filesystem secret provider.
/// </summary>
public static class FileSystemConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:filesystem</c> configuration section to <see cref="FileSystemOptions"/>,
    /// validates the options, and returns the builder.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The secret provider builder.</returns>
    public static SecretProviderBuilder ConfigureFileSystem(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:filesystem").Get<FileSystemOptions>()
            ?? new FileSystemOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for FileSystem: {string.Join("; ", errors)}");

        return builder.WithFileSystem(o =>
        {
            o.BasePath = options.BasePath;
            o.EncryptionKey = options.EncryptionKey;
            o.EncryptionKeyFile = options.EncryptionKeyFile;
        });
    }
}
