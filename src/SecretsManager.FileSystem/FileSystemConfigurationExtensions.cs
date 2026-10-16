using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.FileSystem;

/// <summary>
/// Configuration bridge for [A-Zaaz] provider.
/// </summary>
public static class FileSystemConfigurationExtensions
{
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
