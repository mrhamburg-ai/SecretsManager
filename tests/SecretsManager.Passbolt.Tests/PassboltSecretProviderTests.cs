using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Passbolt.Internal;

namespace SecretsManager.Passbolt.Tests;

public sealed class PassboltSecretProviderTests
{
    private readonly IPassboltApiClient _mockClient = Substitute.For<IPassboltApiClient>();

    private PassboltSecretProvider CreateProvider(string? defaultResourceTypeId = null) =>
        new(_mockClient, defaultResourceTypeId);

    private static PassboltResourceResult MakeResource(
        string id = "resource-id-1",
        string name = "my-secret",
        string? encryptedSecret = null) =>
        new(id, name, null, null, null,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            "resource-type-id-1", false, true, encryptedSecret);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.ListResourcesAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns([MakeResource(encryptedSecret: "{\"password\":\"secret-value\"}")]);

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("1", result.Version);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), result.UpdatedAt);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.ListResourcesAsync("db-config", Arg.Any<CancellationToken>())
            .Returns([MakeResource(name: "db-config", encryptedSecret: "{\"password\":\"{\\\"host\\\":\\\"db.example.com\\\",\\\"port\\\":5432}\"}")]);

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.ListResourcesAsync("missing", Arg.Any<CancellationToken>())
            .Returns([]);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.ListResourcesAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_SecretDoesNotExist_CreatesResource()
    {
        _mockClient.ListResourcesAsync("new-secret", Arg.Any<CancellationToken>())
            .Returns([]);

        _mockClient.GetResourceTypesAsync(Arg.Any<CancellationToken>())
            .Returns([new PassboltResourceTypeResult("rt-1", "password-and-description", "Password with description")]);

        _mockClient.CreateResourceAsync("new-secret", Arg.Any<string>(), "rt-1", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MakeResource(name: "new-secret"));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);

        await _mockClient.Received(1).CreateResourceAsync(
            "new-secret", Arg.Any<string>(), "rt-1", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_SecretExists_UpdatesResource()
    {
        _mockClient.ListResourcesAsync("existing", Arg.Any<CancellationToken>())
            .Returns([MakeResource(id: "res-existing", name: "existing")]);

        _mockClient.UpdateResourceAsync("res-existing", "existing", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MakeResource(id: "res-existing", name: "existing"));

        await using var provider = CreateProvider("rt-1");
        var result = await provider.PutSecretAsync("existing", "updated-value");

        Assert.Equal("updated-value", result.Value);

        await _mockClient.DidNotReceive().CreateResourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockClient.Received(1).UpdateResourceAsync(
            "res-existing", "existing", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesResource()
    {
        _mockClient.ListResourcesAsync("to-delete", Arg.Any<CancellationToken>())
            .Returns([MakeResource(id: "res-delete", name: "to-delete")]);

        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockClient.Received(1).DeleteResourceAsync("res-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.ListResourcesAsync("missing", Arg.Any<CancellationToken>())
            .Returns([]);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsSingleVersion()
    {
        _mockClient.ListResourcesAsync("versioned", Arg.Any<CancellationToken>())
            .Returns([MakeResource(name: "versioned")]);

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Single(versions);
        Assert.True(versions[0].IsCurrent);
        Assert.Equal("1", versions[0].Version);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.ListResourcesAsync("missing", Arg.Any<CancellationToken>())
            .Returns([]);

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenResourceExists()
    {
        _mockClient.ListResourcesAsync("exists", Arg.Any<CancellationToken>())
            .Returns([MakeResource(name: "exists")]);

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockClient.ListResourcesAsync("missing", Arg.Any<CancellationToken>())
            .Returns([]);

        await using var provider = CreateProvider();
        Assert.False(await provider.SecretExistsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ApiError_ThrowsSecretProviderException()
    {
        _mockClient.ListResourcesAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }
}
