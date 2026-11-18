using SecretsManager.Builder;

namespace SecretsManager.Doppler.Tests;

public sealed class DopplerSecretProviderBuilderExtensionsTests
{
    private const string ServiceToken = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789";

    [Fact]
    public void WithDoppler_ReturnsSameBuilder()
    {
        var builder = new SecretProviderBuilder();

        var returned = builder.WithDoppler(options =>
        {
            options.Token = ServiceToken;
        });

        Assert.Same(builder, returned);
    }

    [Fact]
    public async Task WithDoppler_FluentConfiguration_BuildsProvider()
    {
        var builder = new SecretProviderBuilder()
            .WithDoppler(options =>
            {
                options.Token = ServiceToken;
                options.Project = "my-project";
                options.Config = "dev";
                options.UseRawValues = true;
            });

        await using var provider = builder.Build();

        Assert.IsType<DopplerSecretProvider>(provider);
    }

    [Fact]
    public async Task WithDoppler_NoConfiguration_RegistersFactory()
    {
        var builder = new SecretProviderBuilder()
            .WithDoppler()
            .FromYaml(
                """
                provider:
                  doppler:
                    token: dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789
                """);

        await using var provider = builder.Build();

        Assert.IsType<DopplerSecretProvider>(provider);
    }

    [Fact]
    public async Task WithDoppler_YamlUseRawValues_IsAccepted()
    {
        var builder = new SecretProviderBuilder()
            .WithDoppler()
            .FromYaml(
                """
                provider:
                  doppler:
                    token: dp.pt.abcdefghijklmnopqrstuvwxyz0123456789
                    project: my-project
                    config: dev
                    use:
                      raw:
                        values: true
                """);

        await using var provider = builder.Build();

        Assert.IsType<DopplerSecretProvider>(provider);
    }
}
