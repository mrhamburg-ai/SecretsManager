using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;
using SecretsManager.Vault;
using Xunit;

namespace SecretsManager.Vault.Tests;

public class VaultOptionsTests
{
    [Fact]
    public void Validate_ValidOptions_ReturnsNoErrors()
    {
        var options = new VaultOptions { Url = "http://localhost:8200" };
        var errors = options.Validate().ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidUrl_ReturnsError()
    {
        var options = new VaultOptions { Url = "" };
        var errors = options.Validate().ToList();
        Assert.Contains("Url is required", errors);
    }

    [Fact]
    public void Validate_InvalidUriFormat_ReturnsError()
    {
        var options = new VaultOptions { Url = "not-a-uri" };
        var errors = options.Validate().ToList();
        Assert.Contains(errors, e => e.Contains("must start with http:// or https://"));
    }

    [Fact]
    public void ConfigureVault_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Vault:Url"] = "http://vault:8200",
                ["SecretsManager:Vault:Token"] = "my-token",
            })
            .Build();

        var builder = new SecretProviderBuilder();
        builder.ConfigureVault(config);

        var provider = builder.Build();
        Assert.NotNull(provider);
    }

    [Fact]
    public void ConfigureVault_InvalidOptions_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:Vault:Url"] = "",
            })
            .Build();

        var builder = new SecretProviderBuilder();
        Assert.Throws<ArgumentException>(() => builder.ConfigureVault(config));
    }
}
