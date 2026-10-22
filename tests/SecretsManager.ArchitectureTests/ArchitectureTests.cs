using System.Reflection;
using SecretsManager.Builder;
using Xunit;

namespace SecretsManager.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(SecretProviderBuilder).Assembly;
    private static readonly Assembly[] ProviderAssemblies =
    {
        typeof(SecretsManager.AliyunKms.AliyunKmsSecretProviderFactory).Assembly,
        typeof(SecretsManager.AwsSecretsManager.AwsSecretsManagerSecretProviderFactory).Assembly,
        typeof(SecretsManager.AzureKeyVault.AzureKeyVaultSecretProviderFactory).Assembly,
        typeof(SecretsManager.Conjur.ConjurSecretProviderFactory).Assembly,
        typeof(SecretsManager.FileSystem.FileSystemSecretProviderFactory).Assembly,
        typeof(SecretsManager.GoogleSecretManager.GoogleSecretManagerSecretProviderFactory).Assembly,
        typeof(SecretsManager.IBMCloudSecretsManager.IBMCloudSecretsManagerSecretProviderFactory).Assembly,
        typeof(SecretsManager.OVH.OVHSecretProviderFactory).Assembly,
        typeof(SecretsManager.OracleVault.OracleVaultSecretProviderFactory).Assembly,
        typeof(SecretsManager.Passbolt.PassboltSecretProviderFactory).Assembly,
        typeof(SecretsManager.PostgreSql.PostgreSqlSecretProviderFactory).Assembly,
        typeof(SecretsManager.Scaleway.ScalewaySecretProviderFactory).Assembly,
        typeof(SecretsManager.TencentCloud.TencentCloudSecretProviderFactory).Assembly,
        typeof(SecretsManager.Vault.VaultSecretProviderFactory).Assembly,
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
