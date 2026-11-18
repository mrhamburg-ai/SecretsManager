using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.GoogleSecretManager.Internal;

namespace SecretsManager.GoogleSecretManager.Tests;

public sealed class GoogleSecretManagerSecretProviderTests
{
    private readonly ISecretManagerClientWrapper _mockClient = Substitute.For<ISecretManagerClientWrapper>();

    private GoogleSecretManagerSecretProvider CreateProvider() => new(_mockClient);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.AccessSecretVersionAsync("my-secret", null, Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult(
                "my-secret", "secret-value", "1",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
                "text/plain", new Dictionary<string, string> { ["env"] = "prod" }));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("1", result.Version);
        Assert.Equal("text/plain", result.ContentType);
        Assert.NotNull(result.Tags);
        Assert.Equal("prod", result.Tags["env"]);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToClient()
    {
        _mockClient.AccessSecretVersionAsync("my-secret", "2", Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult(
                "my-secret", "old-value", "2",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, null, null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "2" });

        Assert.Equal("2", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.AccessSecretVersionAsync("db-config", null, Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult(
                "db-config", """{"host":"db.example.com","port":5432}""", "1",
                null, null, "application/json", null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithNestedProperty_ExtractsNestedValue()
    {
        _mockClient.AccessSecretVersionAsync("config", null, Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult(
                "config", """{"db":{"host":"db.example.com"}}""", "1",
                null, null, null, null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.AccessSecretVersionAsync("missing", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Not found")));

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.AccessSecretVersionAsync("broken", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Internal, "Internal server error")));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_CreatesSecretAndReturnsValue()
    {
        _mockClient.AddSecretVersionAsync("new-secret", "new-value", null, null, Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult(
                "new-secret", "new-value", "1",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, null, null));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);
    }

    [Fact]
    public async Task PutSecretAsync_WithMetadata_PassesContentTypeAndLabels()
    {
        var labels = new Dictionary<string, string> { ["env"] = "staging" };

        _mockClient.AddSecretVersionAsync(
                "tagged-secret", "value", "application/json",
                Arg.Is<IDictionary<string, string>>(d => d["env"] == "staging"),
                Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult(
                "tagged-secret", "value", "1",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, "application/json", labels));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("tagged-secret", "value",
            new SecretMetadata
            {
                ContentType = "application/json",
                Tags = labels.AsReadOnly()
            });

        Assert.Equal("application/json", result.ContentType);
        Assert.NotNull(result.Tags);
        Assert.Equal("staging", result.Tags["env"]);
    }

    [Fact]
    public async Task PutSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.AddSecretVersionAsync("fail", "v", Arg.Any<string?>(), Arg.Any<IDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.PermissionDenied, "Forbidden")));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("fail", "v"));
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        await using var provider = CreateProvider();
        Assert.Null(await Record.ExceptionAsync(() => provider.DeleteSecretAsync("to-delete")));

        await _mockClient.Received(1).DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.DeleteSecretAsync("missing", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Not found")));

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockClient.AccessSecretVersionAsync("versioned", "latest", Arg.Any<CancellationToken>())
            .Returns(new SecretManagerSecretResult("versioned", "latest", "3", null, null, null, null));

        _mockClient.ListSecretVersionsAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new SecretVersionProperties("1", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "ENABLED"),
                new SecretVersionProperties("2", DateTimeOffset.Parse("2026-02-01T00:00:00Z"), "ENABLED"),
                new SecretVersionProperties("3", DateTimeOffset.Parse("2026-03-01T00:00:00Z"), "ENABLED")));

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(3, versions.Count);
        Assert.False(versions[0].IsCurrent);
        Assert.False(versions[1].IsCurrent);
        Assert.True(versions[2].IsCurrent);
        Assert.Equal("1", versions[0].Version);
        Assert.Equal("3", versions[2].Version);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.AccessSecretVersionAsync("missing", "latest", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Not found")));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _mockClient.SecretExistsAsync("exists", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockClient.SecretExistsAsync("missing", Arg.Any<CancellationToken>())
            .Returns(false);

        await using var provider = CreateProvider();
        Assert.False(await provider.SecretExistsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.SecretExistsAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Internal, "Internal server error")));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }
}
