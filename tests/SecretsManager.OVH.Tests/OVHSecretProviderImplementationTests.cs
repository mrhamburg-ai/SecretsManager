using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.OVH.Internal;

namespace SecretsManager.OVH.Tests;

public sealed class OVHSecretProviderImplementationTests
{
    private readonly IOVHApiClient _mockClient = Substitute.For<IOVHApiClient>();

    private OVHSecretProvider CreateProvider() => new(_mockClient);

    private static OVHSecretResult MakeSecret(string path = "my-secret", int currentVersion = 1) =>
        new(path,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            currentVersion);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("my-secret"));

        _mockClient.AccessVersionAsync("my-secret", 1, Arg.Any<CancellationToken>())
            .Returns(new OVHAccessResult(2, DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "ACTIVE", "secret-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("2", result.Version);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), result.UpdatedAt);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToClient()
    {
        _mockClient.FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("my-secret"));

        _mockClient.AccessVersionAsync("my-secret", 1, Arg.Any<CancellationToken>())
            .Returns(new OVHAccessResult(1, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "ACTIVE", "old-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "1" });

        Assert.Equal("1", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.FindSecretByNameAsync("db-config", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("db-config"));

        _mockClient.AccessVersionAsync("db-config", 1, Arg.Any<CancellationToken>())
            .Returns(new OVHAccessResult(1, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "ACTIVE", """{"host":"db.example.com","port":5432}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((OVHSecretResult?)null);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.FindSecretByNameAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_SecretDoesNotExist_CreatesSecret()
    {
        _mockClient.FindSecretByNameAsync("new-secret", Arg.Any<CancellationToken>())
            .Returns((OVHSecretResult?)null);

        _mockClient.CreateSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("new-secret"));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);

        await _mockClient.Received(1).CreateSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_SecretExists_CreatesNewVersionOnly()
    {
        _mockClient.FindSecretByNameAsync("existing", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("existing"));

        _mockClient.CreateVersionAsync("existing", "updated-value", Arg.Any<CancellationToken>())
            .Returns(new OVHVersionResult(3, DateTimeOffset.Parse("2026-03-01T00:00:00Z"), "ACTIVE"));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("existing", "updated-value");

        Assert.Equal("3", result.Version);
        Assert.Equal("updated-value", result.Value);

        await _mockClient.DidNotReceive().CreateSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        _mockClient.FindSecretByNameAsync("to-delete", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("to-delete"));

        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockClient.Received(1).DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockClient.ListVersionsAsync("versioned", Arg.Any<CancellationToken>())
            .Returns([
                new OVHVersionResult(1, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "DEACTIVATED"),
                new OVHVersionResult(2, DateTimeOffset.Parse("2026-02-01T00:00:00Z"), "DEACTIVATED"),
                new OVHVersionResult(3, DateTimeOffset.Parse("2026-03-01T00:00:00Z"), "ACTIVE")
            ]);

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
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _mockClient.FindSecretByNameAsync("exists", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("exists"));

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((OVHSecretResult?)null);

        await using var provider = CreateProvider();
        Assert.False(await provider.SecretExistsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.FindSecretByNameAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((OVHSecretResult?)null);

        await using var provider = CreateProvider();

        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));
    }

    [Fact]
    public async Task PutSecretAsync_NewSecretWithoutReportedVersion_ReturnsNullVersion()
    {
        _mockClient.FindSecretByNameAsync("new-secret", Arg.Any<CancellationToken>())
            .Returns((OVHSecretResult?)null);

        _mockClient.CreateSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>())
            .Returns(MakeSecret("new-secret", currentVersion: 0));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Null(result.Version);
    }
}
