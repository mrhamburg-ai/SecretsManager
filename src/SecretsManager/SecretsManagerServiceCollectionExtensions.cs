using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecretsManager.Builder;

namespace SecretsManager;

public static class SecretsManagerServiceCollectionExtensions
{
    public static IServiceCollection AddSecretsManager(this IServiceCollection services)
    {
        services.AddTransient<SecretProviderBuilder>();
        return services;
    }

    public static IServiceCollection AddSecretsManager(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<SecretProviderBuilder>();
        services.Configure<SecretsManagerOptions>(configuration.GetSection("SecretsManager"));
        return services;
    }
}

public sealed class SecretsManagerOptions
{
    public string? Provider { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}
