using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.GoogleSecretManager;

/// <summary>
/// Configuration bridge for the Google Cloud Secret Manager secret provider.
/// </summary>
public static class GoogleSecretManagerConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:googlesecretmanager</c> configuration section to <see cref="GoogleSecretManagerOptions"/>,
    /// validates the options, and returns the builder.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The secret provider builder.</returns>
    public static SecretProviderBuilder ConfigureGoogleSecretManager(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:googlesecretmanager").Get<GoogleSecretManagerOptions>()
            ?? new GoogleSecretManagerOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for GoogleSecretManager: {string.Join("; ", errors)}");

        return builder.WithGoogleSecretManager(o =>
        {
            o.ProjectId = options.ProjectId;
            o.CredentialsPath = options.CredentialsPath;
            o.CredentialsJson = options.CredentialsJson;
            o.Endpoint = options.Endpoint;
        });
    }
}
