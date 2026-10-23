using SecretsManager.Builder;

namespace SecretsManager.IBMCloudSecretsManager.Tests;

public sealed class IBMCloudSecretsManagerSecretProviderFactoryTests
{
    [Fact]
    public void ProviderName_ReturnsCorrectName()
    {
        var factory = new IBMCloudSecretsManagerSecretProviderFactory();
        Assert.Equal("ibmcloud-secrets-manager", factory.ProviderName);
    }

    [Fact]
    public void MapConfiguration_AllSettingsProvided_MapsCorrectly()
    {
        var configuration = new Dictionary<string, string>
        {
            ["region"] = "us-south",
            ["instance.id"] = "test-instance-id",
            ["api.key"] = "test-api-key",
            ["service.url"] = "https://custom.example.com"
        };

        var options = IBMCloudSecretsManagerSecretProviderFactory.MapConfiguration(configuration);

        Assert.Equal("us-south", options.Region);
        Assert.Equal("test-instance-id", options.InstanceId);
        Assert.Equal("test-api-key", options.ApiKey);
        Assert.Equal("https://custom.example.com", options.ServiceUrl);
    }

    [Fact]
    public void MapConfiguration_MinimalSettingsProvided_MapsCorrectly()
    {
        var configuration = new Dictionary<string, string>
        {
            ["region"] = "eu-de",
            ["instance.id"] = "test-instance-id",
            ["api.key"] = "test-api-key"
        };

        var options = IBMCloudSecretsManagerSecretProviderFactory.MapConfiguration(configuration);

        Assert.Equal("eu-de", options.Region);
        Assert.Equal("test-instance-id", options.InstanceId);
        Assert.Equal("test-api-key", options.ApiKey);
        Assert.Null(options.ServiceUrl);
    }

    [Fact]
    public void MapConfiguration_EmptySettings_IgnoresNullOrWhiteSpace()
    {
        var configuration = new Dictionary<string, string>
        {
            ["region"] = "",
            ["instance.id"] = "   ",
            ["api.key"] = "test-api-key"
        };

        var options = IBMCloudSecretsManagerSecretProviderFactory.MapConfiguration(configuration);

        Assert.Equal("", options.Region);
        Assert.Equal("", options.InstanceId);
        Assert.Equal("test-api-key", options.ApiKey);
        Assert.Null(options.ServiceUrl);
    }

    [Fact]
    public void CreateProvider_CreatesValidProvider()
    {
        var configuration = new Dictionary<string, string>
        {
            ["region"] = "us-south",
            ["instance.id"] = "test-instance-id",
            ["api.key"] = "test-api-key"
        };

        var factory = new IBMCloudSecretsManagerSecretProviderFactory();
        var provider = factory.Create(configuration);

        Assert.NotNull(provider);
        Assert.IsType<IBMCloudSecretsManagerSecretProvider>(provider);
    }
}