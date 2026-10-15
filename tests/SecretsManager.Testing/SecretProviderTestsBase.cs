using NSubstitute;
using Xunit;

namespace SecretsManager.Testing;

public abstract class SecretProviderTestsBase<T> where T : class
{
    [Fact]
    public virtual void SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        var provider = Substitute.For<T>();
        // Base test — override in derived classes
    }
}
