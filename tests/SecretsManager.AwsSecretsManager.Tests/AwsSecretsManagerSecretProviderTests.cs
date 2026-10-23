using Amazon.SecretsManager.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.AwsSecretsManager.Internal;

namespace SecretsManager.AwsSecretsManager.Tests;

public sealed class AwsSecretsManagerSecretProviderTests
{
    private readonly IAwsSecretsManagerClient _mockClient = Substitute.For<IAwsSecretsManagerClient>();

    private AwsSecretsManagerSecretProvider CreateProvider() => new(_mockClient);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.GetSecretValueAsync("my-secret", null, null, Arg.Any<CancellationToken>())
            .Returns(new AwsSecretResult(
                "my-secret", "secret-value", "v1",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                "text/plain",
                new Dictionary<string, string> { ["env"] = "prod" }));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("v1", result.Version);
        Assert.NotNull(result.Tags);
        Assert.Equal("prod", result.Tags["env"]);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionId()
    {
        _mockClient.GetSecretValueAsync("my-secret", "v2", null, Arg.Any<CancellationToken>())
            .Returns(new AwsSecretResult(
                "my-secret", "old-value", "v2",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "v2" });

        Assert.Equal("v2", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.GetSecretValueAsync("db-config", null, null, Arg.Any<CancellationToken>())
            .Returns(new AwsSecretResult(
                "db-config", """{"host":"db.example.com","port":5432}""", "v1",
                null, "application/json", null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.GetSecretValueAsync("missing", null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ResourceNotFoundException("Secret not found"));

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretValueAsync("broken", null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Internal error"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_CreatesSecretAndReturnsValue()
    {
        _mockClient.PutSecretValueAsync("new-secret", "new-value", null, Arg.Any<CancellationToken>())
            .Returns(new AwsSecretResult(
                "new-secret", "new-value", "v1",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, null));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("v1", result.Version);
    }

    [Fact]
    public async Task PutSecretAsync_WithContentType_PassesContentType()
    {
        _mockClient.PutSecretValueAsync(
                "tagged-secret", "value", "application/json", Arg.Any<CancellationToken>())
            .Returns(new AwsSecretResult(
                "tagged-secret", "value", "v1",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), "application/json", null));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("tagged-secret", "value",
            new SecretMetadata { ContentType = "application/json" });

        Assert.Equal("application/json", result.ContentType);
    }

    [Fact]
    public async Task PutSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.PutSecretValueAsync("fail", "v", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Forbidden"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("fail", "v"));
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockClient.Received(1).DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.DeleteSecretAsync("missing", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ResourceNotFoundException("Secret not found"));

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockClient.ListSecretVersionsAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new AwsSecretVersionInfo("v1", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), false),
                new AwsSecretVersionInfo("v2", DateTimeOffset.Parse("2026-02-01T00:00:00Z"), false),
                new AwsSecretVersionInfo("v3", DateTimeOffset.Parse("2026-03-01T00:00:00Z"), true)));

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(3, versions.Count);
        Assert.False(versions[0].IsCurrent);
        Assert.False(versions[1].IsCurrent);
        Assert.True(versions[2].IsCurrent);
        Assert.Equal("v1", versions[0].Version);
        Assert.Equal("v3", versions[2].Version);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.ListSecretVersionsAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ThrowAsyncEnumerable<AwsSecretVersionInfo>(new SecretNotFoundException("missing")));

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
            .ThrowsAsync(new InvalidOperationException("Internal error"));

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

    private static async IAsyncEnumerable<T> ThrowAsyncEnumerable<T>(Exception ex)
    {
        throw ex;
#pragma warning disable CS0162
        yield return default!;
#pragma warning restore CS0162
    }
}
