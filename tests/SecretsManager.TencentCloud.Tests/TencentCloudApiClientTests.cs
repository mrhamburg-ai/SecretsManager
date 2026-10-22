using System.Net;
using System.Text;
using System.Text.Json;
using SecretsManager.TencentCloud.Internal;

namespace SecretsManager.TencentCloud.Tests;

public sealed class TencentCloudApiClientTests
{
    [Fact]
    public async Task ConcurrentRequests_UsePerRequestHeaders()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://secretsmanager.tencentcloudapi.com/")
        };

        await using var client = new TencentCloudApiClient(
            http, "secret-id", "secret-key", "eu-frankfurt");

        var get = client.GetSecretValueAsync("from-get", null, CancellationToken.None);
        var delete = client.DeleteSecretAsync("from-delete", CancellationToken.None);
        var put = client.PutSecretValueAsync("from-put", "value", "v1", CancellationToken.None);

        await Task.WhenAll(get, delete, put);

        Assert.Equal(3, handler.Requests.Count);

        foreach (var request in handler.Requests)
        {
            Assert.Equal(request.BodyAction, request.HeaderAction);
            Assert.Equal("eu-frankfurt", request.RegionHeader);
            Assert.StartsWith("TC3-HMAC-SHA256", request.Authorization);
            Assert.Contains("secret-id", request.Authorization);
        }

        Assert.Contains(handler.Requests, r => r.BodyAction == "GetSecretValue");
        Assert.Contains(handler.Requests, r => r.BodyAction == "DeleteSecret");
        Assert.Contains(handler.Requests, r => r.BodyAction == "PutSecretValue");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Hold the request briefly so the calls overlap and would expose shared mutable headers.
            await Task.Delay(25, cancellationToken);

            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(body);
            var bodyAction = document.RootElement
                .GetProperty("Action").GetString() ?? "";

            var headerAction = request.Headers.TryGetValues("X-TC-Action", out var actionValues)
                ? string.Join(",", actionValues)
                : "";

            var region = request.Headers.TryGetValues("X-TC-Region", out var regionValues)
                ? string.Join(",", regionValues)
                : "";

            var authorization = request.Headers.TryGetValues("Authorization", out var authValues)
                ? string.Join(",", authValues)
                : "";

            Requests.Add(new CapturedRequest(
                bodyAction, headerAction, region, authorization));

            var json = bodyAction == "GetSecretValue"
                ? """{"SecretId":"id","SecretValue":"value","VersionId":"v1"}"""
                : """{"SecretId":"id","VersionId":"v1"}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(
        string BodyAction,
        string HeaderAction,
        string RegionHeader,
        string Authorization);
}
