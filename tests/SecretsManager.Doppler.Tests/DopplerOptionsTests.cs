namespace SecretsManager.Doppler.Tests;

public sealed class DopplerOptionsTests
{
    [Fact]
    public void Defaults_AreApplied()
    {
        var options = new DopplerOptions();

        Assert.Equal("https://api.doppler.com", options.ApiUrl);
        Assert.Equal("", options.Token);
        Assert.Equal("", options.Project);
        Assert.Equal("", options.Config);
        Assert.False(options.UseRawValues);
    }

    [Fact]
    public void Validate_ServiceTokenWithoutProjectAndConfig_ReturnsNoErrors()
    {
        var options = new DopplerOptions
        {
            Token = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789"
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_ServiceTokenWithProjectAndConfig_ReturnsNoErrors()
    {
        var options = new DopplerOptions
        {
            Token = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789",
            Project = "my-project",
            Config = "dev"
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_PersonalTokenWithProjectAndConfig_ReturnsNoErrors()
    {
        var options = new DopplerOptions
        {
            Token = "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789",
            Project = "my-project",
            Config = "dev"
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_MissingToken_ReturnsError()
    {
        var options = new DopplerOptions();

        Assert.Contains("Token is required", options.Validate());
    }

    [Fact]
    public void Validate_NonServiceTokenMissingProjectAndConfig_ReturnsErrors()
    {
        var options = new DopplerOptions
        {
            Token = "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789"
        };

        var errors = options.Validate().ToList();

        Assert.Contains("Project is required when Token is not a service token", errors);
        Assert.Contains("Config is required when Token is not a service token", errors);
    }

    [Fact]
    public void Validate_BlankApiUrl_ReturnsError()
    {
        var options = new DopplerOptions
        {
            ApiUrl = " ",
            Token = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789"
        };

        Assert.Contains("ApiUrl is required", options.Validate());
    }

    [Fact]
    public void Validate_RelativeApiUrl_ReturnsError()
    {
        var options = new DopplerOptions
        {
            ApiUrl = "api.doppler.com",
            Token = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789"
        };

        Assert.Contains("ApiUrl must be an absolute URI", options.Validate());
    }
}
