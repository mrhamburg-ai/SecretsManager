using System.Reflection;
using SecretsManager.Builder;
using Xunit;

namespace SecretsManager.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(SecretProviderBuilder).Assembly;
    private static readonly Assembly[] ProviderAssemblies =
    {
        typeof(SecretsManager.Vault.VaultSecretProviderFactory).Assembly,
        typeof(SecretsManager.AwsSecretsManager.AwsSecretsManagerSecretProviderFactory).Assembly,
    };

    [Fact]
    public void Core_Should_Not_Reference_Any_Provider()
    {
        var coreRefs = CoreAssembly.GetReferencedAssemblies();
        foreach (var provider in ProviderAssemblies)
        {
            Assert.DoesNotContain(coreRefs, r => r.Name == provider.GetName().Name);
        }
    }

    [Fact]
    public void Providers_Should_Reference_Core()
    {
        foreach (var provider in ProviderAssemblies)
        {
            var refs = provider.GetReferencedAssemblies();
            Assert.Contains(refs, r => r.Name == CoreAssembly.GetName().Name);
        }
    }
}
