namespace SecretsManager.Tests;

public sealed class SecretProviderContractExtensionsTests
{
    [Fact]
    public async Task EnsureSecretExistsAsync_WhenSecretExists_Completes()
    {
        var provider = new DelegatingSecretProvider
        {
            SecretExistsAsyncImpl = (_, _) => Task.FromResult(true)
        };

        var exception = await Record.ExceptionAsync(
            () => provider.EnsureSecretExistsAsync("present"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task EnsureSecretExistsAsync_WhenSecretMissing_ThrowsSecretNotFoundException()
    {
        var provider = new DelegatingSecretProvider
        {
            SecretExistsAsyncImpl = (_, _) => Task.FromResult(false)
        };

        var exception = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.EnsureSecretExistsAsync("missing"));

        Assert.Equal("missing", exception.Key);
    }

    [Fact]
    public async Task EnsureSecretExistsAsync_WhenCheckFails_PropagatesFailure()
    {
        var provider = new DelegatingSecretProvider
        {
            SecretExistsAsyncImpl = (_, _) =>
                Task.FromException<bool>(new SecretProviderException("backend down"))
        };

        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.EnsureSecretExistsAsync("key"));
    }

    [Fact]
    public async Task EnsureSecretExistsAsync_NullProvider_ThrowsArgumentNullException()
    {
        ISecretProvider provider = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.EnsureSecretExistsAsync("key"));
    }
}
