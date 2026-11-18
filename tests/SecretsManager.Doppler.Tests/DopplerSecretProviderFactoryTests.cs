namespace SecretsManager.Doppler.Tests;

public sealed class DopplerSecretProviderFactoryTests
{
    private static readonly Dictionary<string, string> AllFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["api.url"] = "https://doppler.example.com",
        ["token"] = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789",
        ["project"] = "my-project",
        ["config"] = "dev",
        ["use.raw.values"] = "true"
    };

    [Fact]
    public void ProviderName_IsDoppler()
    {
        var factory = new DopplerSecretProviderFactory();

        Assert.Equal("doppler", factory.ProviderName);
    }

    [Fact]
    public void Create_ReturnsDopplerSecretProvider()
    {
        var provider = new DopplerSecretProviderFactory().Create(AllFields);

        Assert.IsType<DopplerSecretProvider>(provider);
    }

    [Fact]
    public void MapConfiguration_WithAllFields_SetsAllOptions()
    {
        var options = DopplerSecretProviderFactory.MapConfiguration(AllFields);

        Assert.Equal("https://doppler.example.com", options.ApiUrl);
        Assert.Equal("dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789", options.Token);
        Assert.Equal("my-project", options.Project);
        Assert.Equal("dev", options.Config);
        Assert.True(options.UseRawValues);
    }

    [Fact]
    public void MapConfiguration_Empty_UsesDefaults()
    {
        var options = DopplerSecretProviderFactory.MapConfiguration(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("https://api.doppler.com", options.ApiUrl);
        Assert.Equal("", options.Token);
        Assert.Equal("", options.Project);
        Assert.Equal("", options.Config);
        Assert.False(options.UseRawValues);
    }

    [Fact]
    public void MapConfiguration_IgnoresWhitespaceValues()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["api.url"] = "  ",
            ["token"] = "  ",
            ["project"] = "  ",
            ["config"] = "  ",
            ["use.raw.values"] = "  "
        };

        var options = DopplerSecretProviderFactory.MapConfiguration(config);

        Assert.Equal("https://api.doppler.com", options.ApiUrl);
        Assert.Equal("", options.Token);
        Assert.Equal("", options.Project);
        Assert.Equal("", options.Config);
        Assert.False(options.UseRawValues);
    }

    [Fact]
    public void MapConfiguration_PartialConfig_SetsOnlyProvidedFields()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["token"] = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789",
            ["config"] = "prd"
        };

        var options = DopplerSecretProviderFactory.MapConfiguration(config);

        Assert.Equal("https://api.doppler.com", options.ApiUrl);
        Assert.Equal("dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789", options.Token);
        Assert.Equal("", options.Project);
        Assert.Equal("prd", options.Config);
        Assert.False(options.UseRawValues);
    }

    [Fact]
    public void MapConfiguration_InvalidUseRawValues_Throws()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["use.raw.values"] = "sometimes"
        };

        var exception = Assert.Throws<ArgumentException>(
            () => DopplerSecretProviderFactory.MapConfiguration(config));

        Assert.Contains("use.raw.values", exception.Message);
    }
}
