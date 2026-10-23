using System.Net;
using System.Text;
using SecretsManager.IBMCloudSecretsManager.Internal;

namespace SecretsManager.IBMCloudSecretsManager.Tests;

public sealed class IBMCloudSecretsManagerApiClientTests
{
    [Fact]
    public async Task FindSecretByNameAsync_ReturnsExactMatchInsteadOfFirstSearchResult()
    {
        const string json = """
            {
              "secrets": [
                {
                  "id": "id-prod",
                  "name": "db-password-prod",
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-02T00:00:00Z",
                  "version_count": 1
                },
                {
                  "id": "id-exact",
                  "name": "db-password",
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-02T00:00:00Z",
                  "version_count": 1
                }
              ]
            }
            """;

        await using var client = CreateClient(json);

        var result = await client.FindSecretByNameAsync("db-password", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("id-exact", result!.Id);
    }

    [Fact]
    public async Task FindSecretByNameAsync_NoExactMatch_ReturnsNull()
    {
        const string json = """
            {
              "secrets": [
                {
                  "id": "id-prod",
                  "name": "db-password-prod",
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-02T00:00:00Z",
                  "version_count": 1
                }
              ]
            }
            """;

        await using var client = CreateClient(json);

        var result = await client.FindSecretByNameAsync("db-password", CancellationToken.None);

        Assert.Null(result);
    }

    private static IBMCloudSecretsManagerApiClient CreateClient(string json)
    {
        var http = new HttpClient(new StubHandler(json));
        return new IBMCloudSecretsManagerApiClient(http, "us-south", "instance-id");
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
