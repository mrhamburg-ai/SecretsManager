using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Oci.Common.Model;
using SecretsManager.OracleVault.Internal;

namespace SecretsManager.OracleVault.Tests;

public sealed class OracleVaultSecretProviderTests
{
    private readonly ISecretClientWrapper _mockClient = Substitute.For<ISecretClientWrapper>();
    private readonly OracleVaultOptions _options = new()
    {
        VaultId = "ocid1.vault.oc1..xxxxx",
        CompartmentId = "ocid1.compartment.oc1..xxxxx",
        Region = "us-phoenix-1"
    };

    private OracleVaultSecretProvider CreateProvider() => new(_mockClient, _options);

    private const string SecretOcid = "ocid1.vaultsecret.oc1.ap-mumbai-1.xxxxx";

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockClient.GetSecretBundleAsync(SecretOcid, null, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(
                SecretOcid, "secret-value", 1, "v1",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                "text/plain",
                new Dictionary<string, string> { ["env"] = "prod" }.AsReadOnly()));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync(SecretOcid);

        Assert.Equal(SecretOcid, result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("1", result.Version);
        Assert.Equal("text/plain", result.ContentType);
        Assert.NotNull(result.Tags);
        Assert.Equal("prod", result.Tags["env"]);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToClient()
    {
        _mockClient.GetSecretBundleAsync(SecretOcid, 2, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(
                SecretOcid, "old-value", 2, "v2",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync(SecretOcid, new SecretQuery { Version = "2" });

        Assert.Equal("2", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockClient.GetSecretBundleAsync(SecretOcid, null, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(
                SecretOcid, """{"host":"db.example.com","port":5432}""", 1, "v1",
                null, "application/json", null));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync(SecretOcid, new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.GetSecretBundleAsync(SecretOcid, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OciException("404 Not found", new Exception()));

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync(SecretOcid));

        Assert.Equal(SecretOcid, ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretBundleAsync(SecretOcid, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OciException("500 Internal server error", new Exception()));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync(SecretOcid));
    }

    [Fact]
    public async Task PutSecretAsync_CreatesSecretAndReturnsValue()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns((OracleSecretSummary?)null);

        _mockClient.CreateSecretAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "new-value",
                Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(
                SecretOcid, "new-value", 1, "new-secret",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, null));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync(SecretOcid, "new-value");

        Assert.Equal(SecretOcid, result.Key);
        Assert.Equal("new-value", result.Value);
    }

    [Fact]
    public async Task PutSecretAsync_UpdatesExistingSecret()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretSummary(SecretOcid, "my-secret"));

        _mockClient.GetSecretBundleAsync(SecretOcid, null, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(SecretOcid, "old", 1, "my-secret", null, null, null));

        _mockClient.UpdateSecretAsync(
                SecretOcid, 1, "updated-value", null, null, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(
                SecretOcid, "updated-value", 2, "my-secret",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null, null));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync(SecretOcid, "updated-value");

        Assert.Equal("updated-value", result.Value);
    }

    [Fact]
    public async Task PutSecretAsync_WithMetadata_PassesContentTypeAndTags()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns((OracleSecretSummary?)null);

        var tags = new Dictionary<string, string> { ["env"] = "staging" };

        _mockClient.CreateSecretAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "value",
                "application/json",
                Arg.Is<IReadOnlyDictionary<string, string>>(d => d["env"] == "staging"),
                Arg.Any<CancellationToken>())
            .Returns(new OracleSecretResult(
                SecretOcid, "value", 1, "my-secret",
                DateTimeOffset.Parse("2026-03-01T00:00:00Z"), "application/json", tags.AsReadOnly()));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync(SecretOcid, "value",
            new SecretMetadata
            {
                ContentType = "application/json",
                Tags = tags.AsReadOnly()
            });

        Assert.Equal("application/json", result.ContentType);
        Assert.NotNull(result.Tags);
        Assert.Equal("staging", result.Tags["env"]);
    }

    [Fact]
    public async Task PutSecretAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns((OracleSecretSummary?)null);

        _mockClient.CreateSecretAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "v",
                Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OciException("403 Forbidden", new Exception()));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync(SecretOcid, "v"));
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        await using var provider = CreateProvider();
        Assert.Null(await Record.ExceptionAsync(() => provider.DeleteSecretAsync(SecretOcid)));

        await _mockClient.Received(1).DeleteSecretAsync(SecretOcid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockClient.DeleteSecretAsync(SecretOcid, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OciException("404 Not found", new Exception()));

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync(SecretOcid));

        Assert.Equal(SecretOcid, ex.Key);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretSummary(SecretOcid, "my-secret"));

        _mockClient.GetSecretVersionsAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new OracleSecretVersionInfo(1, "v1", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), false),
                new OracleSecretVersionInfo(2, "v2", DateTimeOffset.Parse("2026-02-01T00:00:00Z"), false),
                new OracleSecretVersionInfo(3, "v3", DateTimeOffset.Parse("2026-03-01T00:00:00Z"), true)));

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync(SecretOcid);

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
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OciException("404 Not found", new Exception()));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync(SecretOcid));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns(new OracleSecretSummary(SecretOcid, "my-secret"));

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync(SecretOcid));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .Returns((OracleSecretSummary?)null);

        await using var provider = CreateProvider();
        Assert.False(await provider.SecretExistsAsync(SecretOcid));
    }

    [Fact]
    public async Task SecretExistsAsync_ServerError_ThrowsSecretProviderException()
    {
        _mockClient.GetSecretSummaryAsync(SecretOcid, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OciException("500 Internal server error", new Exception()));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync(SecretOcid));
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
