using SecretsManager.Builder;

namespace SecretsManager.Testing;

public static class TestSecretsManagerBuilder
{
    public static SecretProviderBuilder New()
    {
        return new SecretProviderBuilder();
    }
}
