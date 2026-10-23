using System.Net;
using SecretsManager.Vault.Internal;

namespace SecretsManager.Vault.Tests;

public sealed class VaultApiClientTests
{
    [Fact]
    public async Task SecretExistsAsync_NotFound_ReturnsFalse()
    {
        await using var client = CreateClient(HttpStatusCode.NotFound);

        Assert.False(await client.SecretExistsAsync("my-secret", CancellationToken.None));
    }

    [Fact]
    public async Task SecretExistsAsync_Success_ReturnsTrue()
    {
        await using var client = CreateClient(HttpStatusCode.OK);

        Assert.True(await client.SecretExistsAsync("my-secret", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SecretExistsAsync_ServerError_Throws(HttpStatusCode status)
    {
        await using var client = CreateClient(status);

        await Assert.ThrowsAsync<SecretProviderException>(
            () => client.SecretExistsAsync("my-secret", CancellationToken.None));
    }

    private static VaultApiClient CreateClient(HttpStatusCode status)
    {
        var http = new HttpClient(new StubHandler(status))
        {
            BaseAddress = new Uri("https://vault.test/")
        };

        return new VaultApiClient(http, "secret");
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }
}
