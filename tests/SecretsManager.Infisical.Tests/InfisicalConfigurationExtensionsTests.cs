using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Infisical.Tests;

public sealed class InfisicalConfigurationExtensionsTests
{
    [Fact]
    public void ConfigureInfisical_ValidConfiguration_BuildsProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Infisical:HostUri"] = "https://infisical.example.com",
                ["SecretsManager:Infisical:ClientId"] = "client-id",
                ["SecretsManager:Infisical:ClientSecret"] = "client-secret",
                ["SecretsManager:Infisical:ProjectId"] = "project-id",
                ["SecretsManager:Infisical:EnvironmentSlug"] = "production",
                ["SecretsManager:Infisical:SecretPath"] = "/app"
            })
            .Build();

        var builder = new SecretProviderBuilder()
            .ConfigureInfisical(configuration);

        var provider = builder.Build();

        Assert.IsType<InfisicalSecretProvider>(provider);
    }

    [Fact]
    public void ConfigureInfisical_MissingRequiredConfiguration_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Infisical:HostUri"] = "https://infisical.example.com"
            })
            .Build();

        var builder = new SecretProviderBuilder();

        var exception = Assert.Throws<ArgumentException>(
            () => builder.ConfigureInfisical(configuration));

        Assert.Contains("Configuration errors for Infisical", exception.Message);
        Assert.Contains("ClientId is required", exception.Message);
        Assert.Contains("ClientSecret is required", exception.Message);
        Assert.Contains("ProjectId is required", exception.Message);
    }
}
