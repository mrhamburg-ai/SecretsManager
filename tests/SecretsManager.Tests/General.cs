namespace SecretsManager.Tests;

public class SecretExceptionTests
{
    [Fact]
    public void SecretNotFoundException_ContainsKey()
    {
        var ex = new SecretNotFoundException("my-secret");

        Assert.Equal("my-secret", ex.Key);
        Assert.Contains("my-secret", ex.Message);
    }

    [Fact]
    public void SecretNotFoundException_IsSecretProviderException()
    {
        var ex = new SecretNotFoundException("key");

        Assert.IsAssignableFrom<SecretProviderException>(ex);
    }

    [Fact]
    public void SecretNotFoundException_WithInnerException_Wraps()
    {
        var inner = new InvalidOperationException("gone");
        var ex = new SecretNotFoundException("key", inner);

        Assert.Same(inner, ex.InnerException);
    }
}
