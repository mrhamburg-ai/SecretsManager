using System.Net;
using SecretsManager.Conjur.Internal;

namespace SecretsManager.Conjur.Tests;

public sealed class ConjurApiClientTests
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

    private static ConjurApiClient CreateClient(HttpStatusCode resourceStatus)
    {
        var http = new HttpClient(new StubHandler(resourceStatus))
        {
            BaseAddress = new Uri("https://conjur.test/")
        };

        var options = new ConjurOptions
        {
            Account = "myorg",
            Login = "host/app",
            ApiKey = "api-key"
        };

        return new ConjurApiClient(http, options);
    }

    [Fact]
    public async Task DeleteSecretAsync_NestedPolicyPath_StripsPolicyPrefix()
    {
        var handler = new CapturingPolicyHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://conjur.test/")
        };
        var options = new ConjurOptions
        {
            Account = "myorg",
            Login = "host/app",
            ApiKey = "api-key",
            PolicyPath = "team/app"
        };

        await using var client = new ConjurApiClient(http, options);
        await client.DeleteSecretAsync("team/app/db", CancellationToken.None);

        Assert.NotNull(handler.PolicyBody);
        Assert.Contains("record: !variable db", handler.PolicyBody);
        Assert.DoesNotContain("team/app/team/app", handler.PolicyBody);
    }

    private sealed class CapturingPolicyHandler : HttpMessageHandler
    {
        public string? PolicyBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Patch && request.Content is not null)
                PolicyBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("token")
            };
        }
    }

    private sealed class StubHandler(HttpStatusCode resourceStatus) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("/authn/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("token")
                });

            return Task.FromResult(new HttpResponseMessage(resourceStatus));
        }
    }
}
