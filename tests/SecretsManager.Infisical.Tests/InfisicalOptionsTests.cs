namespace SecretsManager.Infisical.Tests;

public sealed class InfisicalOptionsTests
{
    [Fact]
    public void Defaults_AreApplied()
    {
        var options = new InfisicalOptions();

        Assert.Equal("https://app.infisical.com", options.HostUri);
        Assert.Equal("dev", options.EnvironmentSlug);
        Assert.Equal("/", options.SecretPath);
        Assert.Equal("", options.ClientId);
        Assert.Equal("", options.ClientSecret);
        Assert.Equal("", options.ProjectId);
    }

    [Fact]
    public void Validate_ValidOptions_ReturnsNoErrors()
    {
        var options = new InfisicalOptions
        {
            HostUri = "https://infisical.example.com",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            ProjectId = "project-id",
            EnvironmentSlug = "production",
            SecretPath = "/app"
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_MissingRequiredValues_ReturnsErrors()
    {
        var options = new InfisicalOptions
        {
            HostUri = " ",
            ClientId = "",
            ClientSecret = " ",
            ProjectId = "",
            EnvironmentSlug = "",
            SecretPath = ""
        };

        var errors = options.Validate().ToList();

        Assert.Contains("HostUri is required", errors);
        Assert.Contains("ClientId is required", errors);
        Assert.Contains("ClientSecret is required", errors);
        Assert.Contains("ProjectId is required", errors);
        Assert.Contains("EnvironmentSlug is required", errors);
        Assert.Contains("SecretPath is required", errors);
    }
}
