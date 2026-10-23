using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Scaleway.Internal;

namespace SecretsManager.Scaleway.Tests;

public sealed class ScalewaySecretProviderTests
{
    private readonly IScalewayApiClient _mockClient = Substitute.For<IScalewayApiClient>();

    private ScalewaySecretProvider CreateProvider() => new(_mockClient);

    private static ScalewaySecretResult MakeSecret(string id = "secret-id-1", string name = "my-secret") =>
        new(id, name,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            ["env:prod"], 2);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret());

        _mockClient.AccessVersionAsync("secret-id-1", "latest_enabled", Arg.Any<CancellationToken>())
            .Returns(new ScalewayAccessResult("secret-id-1", 2, "secret-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("2", result.Version);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), result.UpdatedAt);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesRevisionToClient()
    {
        _mockClient.FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret());

        _mockClient.AccessVersionAsync("secret-id-1", "1", Arg.Any<CancellationToken>())
            .Returns(new ScalewayAccessResult("secret-id-1", 1, "old-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "1" });

        Assert.Equal("1", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.FindSecretByNameAsync("db-config", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "db-config"));

        _mockClient.AccessVersionAsync("secret-id-1", "latest_enabled", Arg.Any<CancellationToken>())
            .Returns(new ScalewayAccessResult("secret-id-1", 1, """{"host":"db.example.com","port":5432}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithNestedProperty_ExtractsNestedValue()
    {
        _mockClient.FindSecretByNameAsync("config", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "config"));

        _mockClient.AccessVersionAsync("secret-id-1", "latest_enabled", Arg.Any<CancellationToken>())
            .Returns(new ScalewayAccessResult("secret-id-1", 1, """{"db":{"host":"db.example.com"}}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScalewaySecretResult?)null);

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
    public async Task PutSecretAsync_SecretDoesNotExist_CreatesSecretAndVersion()
    {
        _mockClient.FindSecretByNameAsync("new-secret", Arg.Any<CancellationToken>())
            .Returns((ScalewaySecretResult?)null);

        _mockClient.CreateSecretAsync("new-secret", Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "new-secret"));

        _mockClient.CreateVersionAsync("secret-id-1", "new-value", Arg.Any<CancellationToken>())
            .Returns(new ScalewayVersionResult(1, "secret-id-1", "enabled",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, true));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);

        await _mockClient.Received(1).CreateSecretAsync("new-secret", Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
        await _mockClient.Received(1).CreateVersionAsync("secret-id-1", "new-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_SecretExists_CreatesNewVersionOnly()
    {
        _mockClient.FindSecretByNameAsync("existing", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "existing"));

        _mockClient.CreateVersionAsync("secret-id-1", "updated-value", Arg.Any<CancellationToken>())
            .Returns(new ScalewayVersionResult(3, "secret-id-1", "enabled",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, true));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("existing", "updated-value");

        Assert.Equal("3", result.Version);
        Assert.Equal("updated-value", result.Value);

        await _mockClient.DidNotReceive().CreateSecretAsync(Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_WithMetadata_PassesTagKeys()
    {
        _mockClient.FindSecretByNameAsync("tagged", Arg.Any<CancellationToken>())
            .Returns((ScalewaySecretResult?)null);

        var tags = new Dictionary<string, string> { ["env"] = "staging", ["team"] = "platform" };

        _mockClient.CreateSecretAsync("tagged",
                Arg.Is<string[]?>(t => t != null && t.Contains("env") && t.Contains("team")),
                Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "tagged"));

        _mockClient.CreateVersionAsync("secret-id-1", "val", Arg.Any<CancellationToken>())
            .Returns(new ScalewayVersionResult(1, "secret-id-1", "enabled",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, true));

        await using var provider = CreateProvider();
        await provider.PutSecretAsync("tagged", "val",
            new SecretMetadata { Tags = tags.AsReadOnly() });

        await _mockClient.Received(1).CreateSecretAsync("tagged",
            Arg.Is<string[]?>(t => t != null && t.Length == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        _mockClient.FindSecretByNameAsync("to-delete", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "to-delete"));

        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockClient.Received(1).DeleteSecretAsync("secret-id-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScalewaySecretResult?)null);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockClient.FindSecretByNameAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "versioned"));

        _mockClient.ListVersionsAsync("secret-id-1", Arg.Any<CancellationToken>())
            .Returns([
                new ScalewayVersionResult(1, "secret-id-1", "enabled",
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, false),
                new ScalewayVersionResult(2, "secret-id-1", "enabled",
                    DateTimeOffset.Parse("2026-02-01T00:00:00Z"), null, false),
                new ScalewayVersionResult(3, "secret-id-1", "enabled",
                    DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, true)
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
    public async Task GetSecretVersionsAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScalewaySecretResult?)null);

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _mockClient.FindSecretByNameAsync("exists", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(name: "exists"));

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockClient.FindSecretByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScalewaySecretResult?)null);

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
}
