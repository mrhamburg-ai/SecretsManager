using SecretsManager.Builder;

namespace SecretsManager.Infisical.Tests;

public sealed class InfisicalSecretProviderBuilderExtensionsTests
{
    [Fact]
    public void WithInfisical_ReturnsSameBuilder()
    {
        var builder = new SecretProviderBuilder();

        var returned = builder.WithInfisical(options =>
        {
            options.ClientId = "client-id";
            options.ClientSecret = "client-secret";
            options.ProjectId = "project-id";
        });

        Assert.Same(builder, returned);
    }

    [Fact]
    public async Task WithInfisical_FluentConfiguration_BuildsProvider()
    {
        var builder = new SecretProviderBuilder()
            .WithInfisical(options =>
            {
                options.HostUri = "http://localhost:8080";
                options.ClientId = "client-id";
                options.ClientSecret = "client-secret";
                options.ProjectId = "project-id";
            });

        await using var provider = builder.Build();

        Assert.IsType<InfisicalSecretProvider>(provider);
    }

    [Fact]
    public async Task WithInfisical_NoConfiguration_RegistersFactory()
    {
        var builder = new SecretProviderBuilder()
            .WithInfisical()
            .FromYaml(
                """
                provider:
                  infisical:
                    host:
                      uri: http://localhost:8080
                    auth:
                      client:
                        id: client-id
                        secret: client-secret
                    project:
                      id: project-id
                """);

        await using var provider = builder.Build();

        Assert.IsType<InfisicalSecretProvider>(provider);
    }
}
