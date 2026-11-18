using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Doppler;

/// <summary>
/// Binds <see cref="DopplerOptions"/> from configuration and registers the Doppler provider
/// with <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class DopplerConfigurationExtensions
{
    /// <summary>
    /// Reads the <c>SecretsManager:Doppler</c> configuration section, validates it and registers
    /// the Doppler provider.
    /// </summary>
    /// <param name="builder">The builder to register the provider on.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the bound options are invalid.</exception>
    public static SecretProviderBuilder ConfigureDoppler(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:Doppler").Get<DopplerOptions>()
            ?? new DopplerOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Doppler: {string.Join("; ", errors)}");

        return builder.WithDoppler(o =>
        {
            o.ApiUrl = options.ApiUrl;
            o.Token = options.Token;
            o.Project = options.Project;
            o.Config = options.Config;
            o.UseRawValues = options.UseRawValues;
        });
    }
}
