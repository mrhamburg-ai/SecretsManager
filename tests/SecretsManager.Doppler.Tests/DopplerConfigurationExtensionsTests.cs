using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.Doppler.Tests;

public sealed class DopplerConfigurationExtensionsTests
{
    [Fact]
    public async Task ConfigureDoppler_ServiceToken_BuildsProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Doppler:ApiUrl"] = "https://api.doppler.com",
                ["SecretsManager:Doppler:Token"] = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789",
                ["SecretsManager:Doppler:UseRawValues"] = "true"
            })
            .Build();

        var builder = new SecretProviderBuilder()
            .ConfigureDoppler(configuration);

        await using var provider = builder.Build();

        Assert.IsType<DopplerSecretProvider>(provider);
    }

    [Fact]
    public async Task ConfigureDoppler_PersonalTokenWithProjectAndConfig_BuildsProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Doppler:Token"] = "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789",
                ["SecretsManager:Doppler:Project"] = "my-project",
                ["SecretsManager:Doppler:Config"] = "dev"
            })
            .Build();

        var builder = new SecretProviderBuilder()
            .ConfigureDoppler(configuration);

        await using var provider = builder.Build();

        Assert.IsType<DopplerSecretProvider>(provider);
    }

    [Fact]
    public void ConfigureDoppler_MissingToken_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var builder = new SecretProviderBuilder();

        var exception = Assert.Throws<ArgumentException>(
            () => builder.ConfigureDoppler(configuration));

        Assert.Contains("Configuration errors for Doppler", exception.Message);
        Assert.Contains("Token is required", exception.Message);
    }

    [Fact]
    public void ConfigureDoppler_PersonalTokenMissingProjectAndConfig_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Doppler:Token"] = "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789"
            })
            .Build();

        var builder = new SecretProviderBuilder();

        var exception = Assert.Throws<ArgumentException>(
            () => builder.ConfigureDoppler(configuration));

        Assert.Contains("Project is required when Token is not a service token", exception.Message);
        Assert.Contains("Config is required when Token is not a service token", exception.Message);
    }
}
