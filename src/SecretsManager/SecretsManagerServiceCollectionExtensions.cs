using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecretsManager.Builder;

namespace SecretsManager;

/// <summary>
/// Extension methods for registering SecretsManager in dependency injection.
/// </summary>
public static class SecretsManagerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SecretProviderBuilder"/> as a transient service so it can be injected
    /// and configured with the fluent API or the <c>With*</c> extension methods.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSecretsManager(this IServiceCollection services)
    {
        services.AddTransient<SecretProviderBuilder>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="SecretProviderBuilder"/> as a transient service and binds
    /// <see cref="SecretsManagerOptions"/> from the <c>SecretsManager</c> configuration section.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSecretsManager(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<SecretProviderBuilder>();
        services.Configure<SecretsManagerOptions>(configuration.GetSection("SecretsManager"));
        return services;
    }
}

/// <summary>
/// Options bound from the <c>SecretsManager</c> configuration section when
/// <see cref="SecretsManagerServiceCollectionExtensions.AddSecretsManager(IServiceCollection, IConfiguration)"/>
/// is used.
/// </summary>
public sealed class SecretsManagerOptions
{
    /// <summary>
    /// Name of the provider to resolve, matching a registered provider name (for example <c>filesystem</c>).
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Flat settings for the selected provider, using dot-separated keys for nesting.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
