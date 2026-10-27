using System.Net;
using System.Net.Sockets;
using System.Text;
using SecretsManager.Infisical.Internal;

namespace SecretsManager.Infisical.Tests;

public sealed class InfisicalApiClientTests
{
    private const string LoginResponse =
        """{"accessToken":"token","expiresIn":3600,"accessTokenMaxTTL":3600,"tokenType":"Bearer"}""";

    private const string SecretResponse =
        """{"secret":{"secretKey":"my-secret","secretValue":"secret-value","version":2,"secretMetadata":[{"key":"env","value":"dev"}]}}""";

    [Fact]
    public async Task GetSecretAsync_ReturnsValueAndMetadata()
    {
        await using var server = new StubHttpServer((request, _) =>
            request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, LoginResponse)
                : Response(HttpStatusCode.OK, SecretResponse));

        await using var client = CreateClient(server.Url);
        var result = await client.GetSecretAsync("my-secret", null, true, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal(2, result.Version);
        Assert.Equal("dev", result.Metadata!["env"]);
    }

    [Fact]
    public async Task FindSecretAsync_ReturnsSecretFromList()
    {
        await using var server = new StubHttpServer((request, _) =>
            request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, LoginResponse)
                : Response(
                    HttpStatusCode.OK,
                    """{"secrets":[{"secretKey":"my-secret","secretValue":"","version":2,"secretMetadata":[{"key":"env","value":"dev"}]}]}"""));

        await using var client = CreateClient(server.Url);
        var result = await client.FindSecretAsync("my-secret", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("my-secret", result.Key);
        Assert.Equal(2, result.Version);
        Assert.Equal("dev", result.Metadata!["env"]);
    }

    [Fact]
    public async Task FindSecretAsync_SendsConfiguredScopeAndFiltersByKey()
    {
        string? query = null;
        await using var server = new StubHttpServer((request, _) =>
        {
            if (request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal))
                return Response(HttpStatusCode.OK, LoginResponse);

            query = request.Url.Query;
            return Response(
                HttpStatusCode.OK,
                """{"secrets":[{"secretKey":"other","secretValue":"","version":1},{"secretKey":"my-secret","secretValue":"","version":2,"secretMetadata":[{"key":"env","value":"dev"}]}]}""");
        });

        await using var client = CreateClient(server.Url);
        var result = await client.FindSecretAsync("my-secret", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("my-secret", result.Key);
        Assert.NotNull(query);
        Assert.Contains("workspaceId=project-id", query, StringComparison.Ordinal);
        Assert.Contains("environment=dev", query, StringComparison.Ordinal);
        Assert.Contains("secretPath=%2F", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("viewSecretValue=false", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSecretAsync_WithoutValue_SendsVersionAndHidesValue()
    {
        string? query = null;
        await using var server = new StubHttpServer((request, _) =>
        {
            if (request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal))
                return Response(HttpStatusCode.OK, LoginResponse);

            query = request.Url.Query;
            return Response(HttpStatusCode.OK, SecretResponse);
        });

        await using var client = CreateClient(server.Url);
        var result = await client.GetSecretAsync("my-secret", 2, false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(query);
        Assert.Contains("version=2", query, StringComparison.Ordinal);
        Assert.Contains("viewSecretValue=false", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindSecretAsync_Cancelled_ThrowsOperationCanceled()
    {
        await using var server = new StubHttpServer((_, _) =>
            Response(HttpStatusCode.OK, """{"secrets":[]}"""));

        await using var client = CreateClient(server.Url);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.FindSecretAsync("my-secret", cancellation.Token));
    }

    [Fact]
    public async Task DeleteSecretAsync_Exists_ReturnsTrue()
    {
        await using var server = new StubHttpServer((request, _) =>
            request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, LoginResponse)
                : Response(HttpStatusCode.OK, SecretResponse));

        await using var client = CreateClient(server.Url);
        var deleted = await client.DeleteSecretAsync("my-secret", CancellationToken.None);

        Assert.True(deleted);
    }

    [Fact]
    public async Task FindSecretAsync_EmptyList_ReturnsNull()
    {
        await using var server = new StubHttpServer((request, _) =>
            request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, LoginResponse)
                : Response(HttpStatusCode.OK, """{"secrets":[]}"""));

        await using var client = CreateClient(server.Url);
        var result = await client.FindSecretAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSecretAsync_SendsMetadataAndSkipsMultilineEncoding()
    {
        string? requestBody = null;
        await using var server = new StubHttpServer((request, body) =>
        {
            if (request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal))
                return Response(HttpStatusCode.OK, LoginResponse);

            requestBody = body;
            return Response(HttpStatusCode.OK, SecretResponse);
        });

        await using var client = CreateClient(server.Url);
        var result = await client.CreateSecretAsync(
            "my-secret",
            "secret-value",
            new Dictionary<string, string> { ["env"] = "test" },
            CancellationToken.None);

        Assert.Equal(2, result.Version);
        Assert.NotNull(requestBody);
        Assert.Contains("\"skipMultilineEncoding\":true", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"secretMetadata\":[{\"key\":\"env\",\"value\":\"test\"}]", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSecretAsync_SendsNewValueAndMetadata()
    {
        string? requestBody = null;
        await using var server = new StubHttpServer((request, body) =>
        {
            if (request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal))
                return Response(HttpStatusCode.OK, LoginResponse);

            requestBody = body;
            return Response(HttpStatusCode.OK, SecretResponse);
        });

        await using var client = CreateClient(server.Url);
        var result = await client.UpdateSecretAsync(
            "my-secret",
            "new-value",
            new Dictionary<string, string> { ["env"] = "test" },
            CancellationToken.None);

        Assert.Equal(2, result.Version);
        Assert.NotNull(requestBody);
        Assert.Contains("\"secretValue\":\"new-value\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"secretMetadata\":[{\"key\":\"env\",\"value\":\"test\"}]", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ReturnsFalse()
    {
        await using var server = new StubHttpServer((request, _) =>
            request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, LoginResponse)
                : Response(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));

        await using var client = CreateClient(server.Url);
        var deleted = await client.DeleteSecretAsync("missing", CancellationToken.None);

        Assert.False(deleted);
    }

    [Fact]
    public async Task CreateSecretAsync_Unauthorized_ReauthenticatesAndRetries()
    {
        var loginCount = 0;
        var createCount = 0;
        await using var server = new StubHttpServer((request, _) =>
        {
            if (request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal))
            {
                loginCount++;
                return Response(HttpStatusCode.OK, LoginResponse);
            }

            createCount++;
            return createCount == 1
                ? Response(HttpStatusCode.Unauthorized, """{"message":"Unauthorized"}""")
                : Response(HttpStatusCode.OK, SecretResponse);
        });

        await using var client = CreateClient(server.Url);
        var result = await client.CreateSecretAsync("my-secret", "secret-value", null, CancellationToken.None);

        Assert.Equal(2, loginCount);
        Assert.Equal(2, createCount);
        Assert.Equal(2, result.Version);
    }

    [Fact]
    public async Task CreateSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        await using var server = new StubHttpServer((request, _) =>
            request.Url!.AbsolutePath.Contains("universal-auth/login", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, LoginResponse)
                : Response(HttpStatusCode.InternalServerError, """{"message":"boom"}"""));

        await using var client = CreateClient(server.Url);

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => client.CreateSecretAsync("my-secret", "secret-value", null, CancellationToken.None));

        Assert.Contains("creating secret 'my-secret'", exception.Message);
    }

    private static InfisicalApiClient CreateClient(string hostUri) =>
        new(new InfisicalOptions
        {
            HostUri = hostUri,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            ProjectId = "project-id"
        });

    private static Task<(HttpStatusCode Status, string Body)> Response(HttpStatusCode status, string body) =>
        Task.FromResult((status, body));

    private sealed class StubHttpServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _loop;
        private readonly Func<HttpListenerRequest, string?, Task<(HttpStatusCode Status, string Body)>> _handler;

        public StubHttpServer(
            Func<HttpListenerRequest, string?, Task<(HttpStatusCode Status, string Body)>> handler)
        {
            _handler = handler;
            Url = $"http://127.0.0.1:{GetFreePort()}";
            _listener.Prefixes.Add($"{Url}/");
            _listener.Start();
            _loop = Task.Run(LoopAsync);
        }

        public string Url { get; }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            _listener.Stop();
            _listener.Close();

            try
            {
                await _loop;
            }
            catch (Exception) when (_cancellation.IsCancellationRequested)
            {
                // Expected when the listener is stopped.
            }

            _cancellation.Dispose();
        }

        private async Task LoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }

                await HandleAsync(context);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            string? body = null;
            if (context.Request.HasEntityBody)
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                body = await reader.ReadToEndAsync();
            }

            var (status, responseBody) = await _handler(context.Request, body);
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;

            var bytes = Encoding.UTF8.GetBytes(responseBody);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
