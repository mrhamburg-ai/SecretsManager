using System.Net;
using System.Text;
using SecretsManager.Doppler.Internal;

namespace SecretsManager.Doppler.Tests;

public sealed class DopplerApiClientTests
{
    private const string ServiceToken = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789";

    [Fact]
    public async Task GetSecretAsync_SendsBearerTokenAndParsesComputedValue()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"name":"my/secret","value":{"raw":"raw-value","computed":"computed-value"}}""");
        await using var client = CreateClient(handler, ServiceToken);

        var result = await client.GetSecretAsync("my/secret", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("computed-value", result.Value);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal(ServiceToken, handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("name=my%2Fsecret", handler.Request.RequestUri!.Query);
    }

    [Fact]
    public async Task GetSecretAsync_UseRawValues_ReturnsRawValue()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"value":{"raw":"${USER}@host","computed":"brian@host"}}""");
        await using var client = CreateClient(handler, ServiceToken, useRawValues: true);

        var result = await client.GetSecretAsync("DATABASE", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("${USER}@host", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_IncludesProjectAndConfigWhenSet()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"value":{"raw":"v","computed":"v"}}""");
        await using var client = CreateClient(
            handler, "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789", project: "my project", config: "dev");

        await client.GetSecretAsync("MY_SECRET", CancellationToken.None);

        var query = handler.Request!.RequestUri!.Query;
        Assert.Contains("project=my%20project", query);
        Assert.Contains("config=dev", query);
    }

    [Fact]
    public async Task GetSecretAsync_OmitsEmptyProjectAndConfig()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"value":{"raw":"v","computed":"v"}}""");
        await using var client = CreateClient(handler, ServiceToken);

        await client.GetSecretAsync("MY_SECRET", CancellationToken.None);

        var query = handler.Request!.RequestUri!.Query;
        Assert.DoesNotContain("project=", query);
        Assert.DoesNotContain("config=", query);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound);
        await using var client = CreateClient(handler, ServiceToken);

        Assert.Null(await client.GetSecretAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task GetSecretAsync_RestrictedValue_Throws()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"value":{"raw":null,"computed":null}}""");
        await using var client = CreateClient(handler, ServiceToken);

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => client.GetSecretAsync("RESTRICTED", CancellationToken.None));

        Assert.Contains("restricted visibility", exception.Message);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_IncludesStatusAndHidesToken()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized);
        await using var client = CreateClient(handler, ServiceToken);

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => client.GetSecretAsync("MY_SECRET", CancellationToken.None));

        Assert.Contains("401", exception.Message);
        Assert.DoesNotContain(ServiceToken, exception.Message);
    }

    [Fact]
    public async Task GetSecretAsync_RateLimited_IncludesRetryAfter()
    {
        var handler = new StubHandler(
            HttpStatusCode.TooManyRequests,
            retryAfter: TimeSpan.FromSeconds(30));
        await using var client = CreateClient(handler, ServiceToken);

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => client.GetSecretAsync("MY_SECRET", CancellationToken.None));

        Assert.Contains("429", exception.Message);
        Assert.Contains("retry after 30s", exception.Message);
    }

    [Fact]
    public async Task PutSecretAsync_SendsUpsertBodyAndParsesStoredValue()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"secrets":{"MY_SECRET":{"raw":"raw-value","computed":"computed-value"}}}""");
        await using var client = CreateClient(
            handler, "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789", project: "my-project", config: "dev");

        var result = await client.PutSecretAsync("MY_SECRET", "new-value", CancellationToken.None);

        Assert.Equal("computed-value", result.Value);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Contains("\"secrets\"", handler.RequestBody);
        Assert.Contains("\"MY_SECRET\":\"new-value\"", handler.RequestBody);
        Assert.Contains("\"project\":\"my-project\"", handler.RequestBody);
        Assert.Contains("\"config\":\"dev\"", handler.RequestBody);
    }

    [Fact]
    public async Task PutSecretAsync_UseRawValues_ReturnsRawValue()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """{"secrets":{"MY_SECRET":{"raw":"raw-value","computed":"computed-value"}}}""");
        await using var client = CreateClient(handler, ServiceToken, useRawValues: true);

        var result = await client.PutSecretAsync("MY_SECRET", "new-value", CancellationToken.None);

        Assert.Equal("raw-value", result.Value);
    }

    [Fact]
    public async Task PutSecretAsync_ResponseWithoutSecret_ReturnsSubmittedValue()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        await using var client = CreateClient(handler, ServiceToken);

        var result = await client.PutSecretAsync("MY_SECRET", "new-value", CancellationToken.None);

        Assert.Equal("new-value", result.Value);
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ReturnsFalse()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound);
        await using var client = CreateClient(handler, ServiceToken);

        Assert.False(await client.DeleteSecretAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSecretAsync_NoContent_ReturnsTrue()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent);
        await using var client = CreateClient(handler, ServiceToken);

        Assert.True(await client.DeleteSecretAsync("my-secret", CancellationToken.None));
        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrueAndFalse()
    {
        var found = new StubHandler(HttpStatusCode.OK);
        await using (var client = CreateClient(found, ServiceToken))
            Assert.True(await client.SecretExistsAsync("my-secret", CancellationToken.None));

        var missing = new StubHandler(HttpStatusCode.NotFound);
        await using (var client = CreateClient(missing, ServiceToken))
            Assert.False(await client.SecretExistsAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task SecretExistsAsync_ApiError_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden);
        await using var client = CreateClient(handler, ServiceToken);

        await Assert.ThrowsAsync<SecretProviderException>(
            () => client.SecretExistsAsync("my-secret", CancellationToken.None));
    }

    [Fact]
    public async Task GetSecretAsync_EmptyKey_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        await using var client = CreateClient(handler, ServiceToken);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetSecretAsync(" ", CancellationToken.None));
    }

    private static DopplerApiClient CreateClient(
        StubHandler handler,
        string token,
        bool useRawValues = false,
        string project = "",
        string config = "")
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.doppler.test/")
        };

        var options = new DopplerOptions
        {
            Token = token,
            Project = project,
            Config = config,
            UseRawValues = useRawValues
        };

        return new DopplerApiClient(http, options);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string? _body;
        private readonly TimeSpan? _retryAfter;

        public StubHandler(
            HttpStatusCode status,
            string? body = null,
            TimeSpan? retryAfter = null)
        {
            _status = status;
            _body = body;
            _retryAfter = retryAfter;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;

            if (request.Content is not null)
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(_status);

            if (_body is not null)
                response.Content = new StringContent(_body, Encoding.UTF8, "application/json");

            if (_retryAfter is { } retryAfter)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);

            return response;
        }
    }
}
