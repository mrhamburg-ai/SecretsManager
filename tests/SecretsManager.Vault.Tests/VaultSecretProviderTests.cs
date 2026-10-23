using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Vault.Internal;

namespace SecretsManager.Vault.Tests;

public sealed class VaultSecretProviderTests
{
    private readonly IVaultApiClient _mockClient = Substitute.For<IVaultApiClient>();

    private VaultSecretProvider CreateProvider() => new(_mockClient);

    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset UpdatedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z");

    private static VaultSecretResult MakeSecret(
        string key = "my-secret",
        string value = "secret-value",
        int version = 2) =>
        new(key, value, version, CreatedAt, UpdatedAt);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.GetSecretAsync("my-secret", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecret());

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("2", result.Version);
        Assert.Equal(CreatedAt, result.CreatedAt);
        Assert.Equal(UpdatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToClient()
    {
        _mockClient.GetSecretAsync("my-secret", 1, Arg.Any<CancellationToken>())
            .Returns(MakeSecret(version: 1, value: "old-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "1" });

        Assert.Equal("1", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.GetSecretAsync("db-config", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "db-config", value: """{"host":"db.example.com","port":5432}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithNestedProperty_ExtractsNestedValue()
    {
        _mockClient.GetSecretAsync("config", null, Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "config", value: """{"db":{"host":"db.example.com"}}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.GetSecretAsync("missing", null, Arg.Any<CancellationToken>())
            .Returns((VaultSecretResult?)null);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretAsync("broken", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_CreatesSecretAndReturnsValue()
    {
        _mockClient.PutSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "new-secret", value: "new-value", version: 1));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);

        await _mockClient.Received(1).PutSecretAsync("new-secret", "new-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_ExistingSecret_CreatesNewVersion()
    {
        _mockClient.PutSecretAsync("existing", "updated-value", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "existing", value: "updated-value", version: 3));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("existing", "updated-value");

        Assert.Equal("3", result.Version);
        Assert.Equal("updated-value", result.Value);
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
            .Returns([
                new VaultVersionResult(1, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), false, false),
                new VaultVersionResult(2, DateTimeOffset.Parse("2026-02-01T00:00:00Z"), false, false),
                new VaultVersionResult(3, DateTimeOffset.Parse("2026-03-01T00:00:00Z"), true, false)
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
    public async Task GetSecretVersionsAsync_FiltersOutDestroyedVersions()
    {
        _mockClient.ListVersionsAsync("partial", Arg.Any<CancellationToken>())
            .Returns([
                new VaultVersionResult(1, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), false, true),
                new VaultVersionResult(2, DateTimeOffset.Parse("2026-02-01T00:00:00Z"), true, false)
            ]);

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("partial");

        Assert.Single(versions);
        Assert.Equal("2", versions[0].Version);
        Assert.True(versions[0].IsCurrent);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_Empty_WhenSecretNotFound()
    {
        _mockClient.ListVersionsAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<VaultVersionResult>());

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
