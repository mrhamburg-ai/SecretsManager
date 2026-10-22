using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SecretsManager.Builder;

namespace SecretsManager;

/// <summary>
/// Extension methods for registering SecretsManager in dependency injection.
/// </summary>
public static class SecretsManagerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SecretProviderBuilder"/> as a transient service so it can be injected
    /// and configured with the fluent API or the <c>With*</c> extension methods. Also registers
    /// <see cref="AmbientSecretActorAccessor.Instance"/> as the default <see cref="ISecretActorAccessor"/>
    /// and an <see cref="ISecretProviderLogger"/> backed by the application's <see cref="ILoggerFactory"/>
    /// (a no-op logger when none is registered).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSecretsManager(this IServiceCollection services)
    {
        services.AddTransient<SecretProviderBuilder>();
        services.TryAddSingleton<ISecretActorAccessor>(AmbientSecretActorAccessor.Instance);
        AddSecretProviderLogger(services);
        return services;
    }

    /// <summary>
    /// Registers <see cref="SecretProviderBuilder"/> as a transient service, binds
    /// <see cref="SecretsManagerOptions"/> from the <c>SecretsManager</c> configuration section, and
    /// registers <see cref="AmbientSecretActorAccessor.Instance"/> as the default
    /// <see cref="ISecretActorAccessor"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSecretsManager(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<SecretProviderBuilder>();
        services.TryAddSingleton<ISecretActorAccessor>(AmbientSecretActorAccessor.Instance);
        AddSecretProviderLogger(services);
        services.Configure<SecretsManagerOptions>(configuration.GetSection("SecretsManager"));
        return services;
    }
    private static void AddSecretProviderLogger(IServiceCollection services)
    {
        // TryAdd: an application-supplied ISecretProviderLogger always wins.
        services.TryAddSingleton<ISecretProviderLogger>(serviceProvider =>
        {
            var factory = serviceProvider.GetService<ILoggerFactory>();
            return factory is null
                ? new NullSecretProviderLogger()
                : new LoggerSecretProviderLogger(factory.CreateLogger("SecretsManager"));
        });
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
