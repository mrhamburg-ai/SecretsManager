using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Conjur.Internal;

namespace SecretsManager.Conjur.Tests;

public sealed class ConjurSecretProviderTests
{
    private readonly IConjurApiClient _mockClient = Substitute.For<IConjurApiClient>();

    private ConjurSecretProvider CreateProvider() => new(_mockClient);

    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    private static IReadOnlyList<ConjurVersionResult> Versions(params (int Version, bool IsCurrent)[] entries) =>
        entries
            .Select(e => new ConjurVersionResult(e.Version, CreatedAt, e.IsCurrent))
            .ToList()
            .AsReadOnly();

    private static void StubCurrentVersion(IConjurApiClient client, int version)
    {
        client.ListVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Versions((version, true)));
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.GetSecretValueAsync("my-secret", null, Arg.Any<CancellationToken>())
            .Returns("secret-value");
        StubCurrentVersion(_mockClient, 2);

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("2", result.Version);
        Assert.Equal(CreatedAt, result.CreatedAt);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToClient()
    {
        _mockClient.GetSecretValueAsync("my-secret", 1, Arg.Any<CancellationToken>())
            .Returns("old-value");
        _mockClient.ListVersionsAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(Versions((1, false), (2, true)));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "1" });

        Assert.Equal("1", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.GetSecretValueAsync("db-config", null, Arg.Any<CancellationToken>())
            .Returns("""{"host":"db.example.com","port":5432}""");
        StubCurrentVersion(_mockClient, 1);

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithNestedProperty_ExtractsNestedValue()
    {
        _mockClient.GetSecretValueAsync("config", null, Arg.Any<CancellationToken>())
            .Returns("""{"db":{"host":"db.example.com"}}""");
        StubCurrentVersion(_mockClient, 1);

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.GetSecretValueAsync("missing", null, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretValueAsync("broken", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_StoresValueAndReturnsNewVersion()
    {
        _mockClient.PutSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        StubCurrentVersion(_mockClient, 1);

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);
        Assert.Equal(CreatedAt, result.CreatedAt);

        await _mockClient.Received(1).PutSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_ExistingSecret_CreatesNewVersion()
    {
        _mockClient.PutSecretAsync("existing", "updated-value", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        StubCurrentVersion(_mockClient, 3);

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("existing", "updated-value");

        Assert.Equal("3", result.Version);
        Assert.Equal("updated-value", result.Value);
    }

    [Fact]
    public async Task PutSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.PutSecretAsync("broken", "value", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("broken", "value"));
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockClient.Received(1).DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.DeleteSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.DeleteSecretAsync("broken"));
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockClient.ListVersionsAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(Versions((1, false), (2, false), (3, true)));

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(3, versions.Count);
        Assert.False(versions[0].IsCurrent);
        Assert.False(versions[1].IsCurrent);
        Assert.True(versions[2].IsCurrent);
        Assert.Equal("1", versions[0].Version);
        Assert.Equal("3", versions[2].Version);
        Assert.Equal(CreatedAt, versions[0].CreatedAt);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_Empty_WhenSecretNotFound()
    {
        _mockClient.ListVersionsAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ConjurVersionResult>());

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("missing");

        Assert.Empty(versions);
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
    public async Task SecretExistsAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.SecretExistsAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }
}