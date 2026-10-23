using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.TencentCloud.Internal;

namespace SecretsManager.TencentCloud.Tests;

public sealed class TencentCloudSecretProviderTests
{
    private readonly ITencentCloudApiClient _mockClient = Substitute.For<ITencentCloudApiClient>();

    private TencentCloudSecretProvider CreateProvider() => new(_mockClient);

    private static TencentCloudSecretValueResult MakeSecretValue(
        string name = "my-secret",
        string data = "secret-value",
        string versionId = "v1",
        DateTimeOffset? createTime = null) =>
        new(name, data, versionId, createTime ?? DateTimeOffset.Parse("2026-01-01T00:00:00Z"), ["Latest"]);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.GetSecretValueAsync("my-secret", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecretValue());

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("v1", result.Version);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), result.CreatedAt);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_UsesVersionId()
    {
        _mockClient.GetSecretValueAsync("my-secret", "v2", Arg.Any<CancellationToken>())
            .Returns(MakeSecretValue(versionId: "v2", data: "old-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "v2" });

        Assert.Equal("old-value", result.Value);
        Assert.Equal("v2", result.Version);

        await _mockClient.Received(1).GetSecretValueAsync("my-secret", "v2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.GetSecretValueAsync("db-config", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecretValue(name: "db-config", data: "{\"host\":\"db.example.com\",\"port\":5432}"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.GetSecretValueAsync("missing", null, Arg.Any<CancellationToken>())
            .Returns((TencentCloudSecretValueResult?)null);

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
    public async Task PutSecretAsync_SecretDoesNotExist_CreatesSecret()
    {
        _mockClient.GetSecretValueAsync("new-secret", null, Arg.Any<CancellationToken>())
            .Returns((TencentCloudSecretValueResult?)null);

        _mockClient.CreateSecretAsync("new-secret", "new-value", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TencentCloudSecretResult("new-secret", "v-abc123"));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("v-abc123", result.Version);

        await _mockClient.Received(1).CreateSecretAsync("new-secret", "new-value", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockClient.DidNotReceive().PutSecretValueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_SecretExists_PutsNewVersion()
    {
        _mockClient.GetSecretValueAsync("existing", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecretValue(name: "existing"));

        _mockClient.PutSecretValueAsync("existing", "updated-value", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TencentCloudSecretResult("existing", "v-new"));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("existing", "updated-value");

        Assert.Equal("updated-value", result.Value);
        Assert.Equal("v-new", result.Version);

        await _mockClient.DidNotReceive().CreateSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockClient.Received(1).PutSecretValueAsync("existing", "updated-value", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockClient.Received(1).DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersions()
    {
        _mockClient.ListSecretVersionIdsAsync("versioned", Arg.Any<CancellationToken>())
            .Returns([
                new TencentCloudVersionResult("v1", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), ["Previous"]),
                new TencentCloudVersionResult("v2", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), ["Latest"])
            ]);

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(2, versions.Count);
        Assert.True(versions[0].IsCurrent);
        Assert.Equal("v2", versions[0].Version);
        Assert.False(versions[1].IsCurrent);
        Assert.Equal("v1", versions[1].Version);
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _mockClient.GetSecretValueAsync("exists", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecretValue(name: "exists"));

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockClient.GetSecretValueAsync("missing", null, Arg.Any<CancellationToken>())
            .Returns((TencentCloudSecretValueResult?)null);

        await using var provider = CreateProvider();
        Assert.False(await provider.SecretExistsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretValueAsync("broken", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }
}
