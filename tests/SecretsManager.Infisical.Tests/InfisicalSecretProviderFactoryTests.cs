namespace SecretsManager.Infisical.Tests;

public sealed class InfisicalSecretProviderFactoryTests
{
    private static readonly Dictionary<string, string> AllFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["host.uri"] = "https://infisical.example.com",
        ["auth.client.id"] = "client-id",
        ["auth.client.secret"] = "client-secret",
        ["project.id"] = "project-id",
        ["environment"] = "production",
        ["secret.path"] = "/app"
    };

    [Fact]
    public void ProviderName_IsInfisical()
    {
        var factory = new InfisicalSecretProviderFactory();

        Assert.Equal("infisical", factory.ProviderName);
    }

    [Fact]
    public async Task Create_ReturnsInfisicalSecretProvider()
    {
        await using var provider = new InfisicalSecretProviderFactory().Create(AllFields);

        Assert.IsType<InfisicalSecretProvider>(provider);
    }

    [Fact]
    public void MapConfiguration_WithAllFields_SetsAllOptions()
    {
        var options = InfisicalSecretProviderFactory.MapConfiguration(AllFields);

        Assert.Equal("https://infisical.example.com", options.HostUri);
        Assert.Equal("client-id", options.ClientId);
        Assert.Equal("client-secret", options.ClientSecret);
        Assert.Equal("project-id", options.ProjectId);
        Assert.Equal("production", options.EnvironmentSlug);
        Assert.Equal("/app", options.SecretPath);
    }

    [Fact]
    public void MapConfiguration_Empty_UsesDefaults()
    {
        var options = InfisicalSecretProviderFactory.MapConfiguration(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("https://app.infisical.com", options.HostUri);
        Assert.Equal("", options.ClientId);
        Assert.Equal("", options.ClientSecret);
        Assert.Equal("", options.ProjectId);
        Assert.Equal("dev", options.EnvironmentSlug);
        Assert.Equal("/", options.SecretPath);
    }

    [Fact]
    public void MapConfiguration_IgnoresWhitespaceValues()
    {
        var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host.uri"] = "  ",
            ["auth.client.id"] = "  ",
            ["auth.client.secret"] = "  ",
            ["project.id"] = "  ",
            ["environment"] = "  ",
            ["secret.path"] = "  "
        };

        var options = InfisicalSecretProviderFactory.MapConfiguration(configuration);

        Assert.Equal("https://app.infisical.com", options.HostUri);
        Assert.Equal("", options.ClientId);
        Assert.Equal("", options.ClientSecret);
        Assert.Equal("", options.ProjectId);
        Assert.Equal("dev", options.EnvironmentSlug);
        Assert.Equal("/", options.SecretPath);
    }

    [Fact]
    public void MapConfiguration_PartialConfig_SetsOnlyProvidedFields()
    {
        var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host.uri"] = "http://localhost:8080",
            ["auth.client.id"] = "client-id",
            ["project.id"] = "project-id"
        };

        var options = InfisicalSecretProviderFactory.MapConfiguration(configuration);

        Assert.Equal("http://localhost:8080", options.HostUri);
        Assert.Equal("client-id", options.ClientId);
        Assert.Equal("", options.ClientSecret);
        Assert.Equal("project-id", options.ProjectId);
        Assert.Equal("dev", options.EnvironmentSlug);
        Assert.Equal("/", options.SecretPath);
    }
}
