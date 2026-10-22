using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SecretsManager.OVH.Internal;

namespace SecretsManager.OVH.Tests;

public sealed class OVHOAuthHttpClientTests
{
    [Fact]
    public async Task FindSecretByNameAsync_SignsAgainstConfiguredEndpoint()
    {
        var handler = new CapturingHandler(HttpStatusCode.NotFound);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://ovh.example.test/")
        };

        await using var client = new OVHOAuthHttpClient(
            http,
            okmsId: "okms-1",
            applicationKey: "app-key",
            applicationSecret: "app-secret",
            consumerKey: "consumer-key");

        await client.FindSecretByNameAsync("my-secret", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("app-key", request.Headers["X-Ovh-Application"]);
        Assert.Equal("consumer-key", request.Headers["X-Ovh-Consumer"]);

        var timestamp = request.Headers["X-Ovh-Timestamp"];
        var expectedSignature = ComputeSignature(
            "app-secret",
            "consumer-key",
            "GET",
            "https://ovh.example.test/v2/okms/resource/okms-1/secret/my-secret",
            timestamp);

        Assert.Equal(expectedSignature, request.Headers["X-Ovh-Signature"]);
    }

    [Fact]
    public async Task FindSecretByNameAsync_ReturnsCurrentVersion()
    {
        const string json = """
            {
              "path": "my-secret",
              "metadata": {
                "createdAt": "2026-01-01T00:00:00Z",
                "updatedAt": "2026-01-02T00:00:00Z",
                "currentVersion": 7
              }
            }
            """;

        var handler = new CapturingHandler(HttpStatusCode.OK, json);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://ovh.example.test/")
        };

        await using var client = new OVHOAuthHttpClient(
            http, "okms-1", "app-key", "app-secret", "consumer-key");

        var result = await client.FindSecretByNameAsync("my-secret", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(7, result.CurrentVersion);
    }

    [Fact]
    public async Task GetSecretAsync_UsesReportedCurrentVersion()
    {
        var mockClient = Substitute.For<IOVHApiClient>();
        mockClient.FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(new OVHSecretResult(
                "my-secret",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-01-02T00:00:00Z", CultureInfo.InvariantCulture),
                CurrentVersion: 4));
        mockClient.AccessVersionAsync("my-secret", 4, Arg.Any<CancellationToken>())
            .Returns(new OVHAccessResult(
                4, DateTimeOffset.Parse("2026-01-02T00:00:00Z", CultureInfo.InvariantCulture), "ACTIVE", "value"));

        await using var provider = new OVHSecretProvider(mockClient);

        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("4", result.Version);
        await mockClient.Received(1).FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>());
        await mockClient.Received(1).AccessVersionAsync("my-secret", 4, Arg.Any<CancellationToken>());
    }

    private static string ComputeSignature(
        string applicationSecret,
        string consumerKey,
        string method,
        string url,
        string timestamp)
    {
        var payload =
            $"{applicationSecret}&{consumerKey}&{method}&{url}&{timestamp}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(payload));
        return "$1$" + Convert.ToBase64String(hash);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public CapturingHandler(HttpStatusCode status, string body = "{}")
        {
            _status = status;
            _body = body;
        }

        public List<CapturedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(
                h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

            Requests.Add(new CapturedRequest(
                headers,
                request.RequestUri!.AbsoluteUri));

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record CapturedRequest(
        IReadOnlyDictionary<string, string> Headers,
        string Url);
}
