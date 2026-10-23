using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.AzureKeyVault.Tests;

public sealed class SecretProviderBuilderExtensionsTests
{
    [Fact]
    public async Task WithAzureKeyVault_FluentConfiguration_BuildsProvider()
    {
        var builder = new SecretProviderBuilder()
            .WithAzureKeyVault(options => options.VaultUrl = "https://example.vault.azure.net/");

        await using var provider = builder.Build();

        Assert.IsType<AzureKeyVaultSecretProvider>(provider);
    }

    [Fact]
    public async Task ConfigureAzureKeyVault_BoundConfiguration_BuildsProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsManager:azurekeyvault:VaultUrl"] = "https://example.vault.azure.net/"
            })
            .Build();

        var builder = new SecretProviderBuilder().ConfigureAzureKeyVault(configuration);

        await using var provider = builder.Build();

        Assert.IsType<AzureKeyVaultSecretProvider>(provider);
    }
}
