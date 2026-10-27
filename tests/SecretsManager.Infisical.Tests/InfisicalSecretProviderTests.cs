using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Infisical.Internal;

namespace SecretsManager.Infisical.Tests;

public sealed class InfisicalSecretProviderTests
{
    private readonly IInfisicalApiClient _client = Substitute.For<IInfisicalApiClient>();

    private InfisicalSecretProvider CreateProvider() => new(_client);

    private static InfisicalSecretResult Result(
        string key,
        string value,
        int version,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new(key, value, version, metadata);

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InfisicalSecretProvider((InfisicalOptions)null!));
    }

    [Theory]
    [InlineData("", "client-secret", "project-id", "ClientId is required")]
    [InlineData("client-id", "", "project-id", "ClientSecret is required")]
    [InlineData("client-id", "client-secret", "", "ProjectId is required")]
    public void Constructor_MissingRequiredOption_Throws(
        string clientId,
        string clientSecret,
        string projectId,
        string expectedMessage)
    {
        var options = new InfisicalOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ProjectId = projectId
        };

        var exception = Assert.Throws<ArgumentException>(() => new InfisicalSecretProvider(options));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValueAndMetadata()
    {
        var metadata = new Dictionary<string, string> { ["env"] = "dev" };
        _client.GetSecretAsync("my-secret", null, true, Arg.Any<CancellationToken>())
            .Returns(Result("my-secret", "secret-value", 2, metadata));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("2", result.Version);
        Assert.Equal(metadata, result.Tags);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToClient()
    {
        _client.GetSecretAsync("my-secret", 1, true, Arg.Any<CancellationToken>())
            .Returns(Result("my-secret", "old-value", 1));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "1" });

        Assert.Equal("1", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithInvalidVersion_PassesNullToClient()
    {
        _client.GetSecretAsync("my-secret", null, true, Arg.Any<CancellationToken>())
            .Returns(Result("my-secret", "current-value", 3));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "not-a-number" });

        Assert.Equal("3", result.Version);
        await _client.Received(1)
            .GetSecretAsync("my-secret", null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _client.GetSecretAsync("db-config", null, true, Arg.Any<CancellationToken>())
            .Returns(Result("db-config", """{"db":{"host":"db.example.com"}}""", 1));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _client.GetSecretAsync("missing", null, true, Arg.Any<CancellationToken>())
            .Returns((InfisicalSecretResult?)null);

        await using var provider = CreateProvider();
        var exception = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", exception.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.GetSecretAsync("broken", null, true, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task PutSecretAsync_NewSecret_CreatesSecret()
    {
        var metadata = new Dictionary<string, string> { ["env"] = "test" };
        _client.FindSecretAsync("new-secret", Arg.Any<CancellationToken>())
            .Returns((InfisicalSecretResult?)null);
        _client.CreateSecretAsync("new-secret", "new-value", metadata, Arg.Any<CancellationToken>())
            .Returns(Result("new-secret", "new-value", 1, metadata));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync(
            "new-secret",
            "new-value",
            new SecretMetadata { Tags = metadata });

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);
        Assert.Equal(metadata, result.Tags);

        await _client.Received(1)
            .CreateSecretAsync("new-secret", "new-value", metadata, Arg.Any<CancellationToken>());
        await _client.DidNotReceiveWithAnyArgs()
            .UpdateSecretAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task PutSecretAsync_ExistingSecret_UpdatesSecret()
    {
        _client.FindSecretAsync("existing", Arg.Any<CancellationToken>())
            .Returns(Result("existing", "old-value", 1));
        _client.UpdateSecretAsync("existing", "new-value", null, Arg.Any<CancellationToken>())
            .Returns(Result("existing", "new-value", 2));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("existing", "new-value");

        Assert.Equal("2", result.Version);
        Assert.Equal("new-value", result.Value);

        await _client.Received(1)
            .UpdateSecretAsync("existing", "new-value", null, Arg.Any<CancellationToken>());
        await _client.DidNotReceiveWithAnyArgs()
            .CreateSecretAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task PutSecretAsync_MapsTagsToMetadata()
    {
        var tags = new Dictionary<string, string>
        {
            ["env"] = "production",
            ["owner"] = "platform"
        };

        _client.FindSecretAsync("tagged", Arg.Any<CancellationToken>())
            .Returns((InfisicalSecretResult?)null);
        _client.CreateSecretAsync(
                "tagged",
                "value",
                Arg.Is<IReadOnlyDictionary<string, string>?>(m => m != null && m["env"] == "production" && m["owner"] == "platform"),
                Arg.Any<CancellationToken>())
            .Returns(Result("tagged", "value", 1, tags));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync(
            "tagged",
            "value",
            new SecretMetadata { Tags = tags });

        Assert.Equal(tags, result.Tags);
    }

    [Fact]
    public async Task PutSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.FindSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new SecretProviderException("boom"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("broken", "value"));
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        _client.DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _client.Received(1)
            .DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _client.DeleteSecretAsync("missing", Arg.Any<CancellationToken>())
            .Returns(false);

        await using var provider = CreateProvider();
        var exception = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));

        Assert.Equal("missing", exception.Key);
    }

    [Fact]
    public async Task DeleteSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.DeleteSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.DeleteSecretAsync("broken"));
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _client.FindSecretAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 3));
        _client.GetSecretAsync("versioned", 1, false, Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 1));
        _client.GetSecretAsync("versioned", 2, false, Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 2));
        _client.GetSecretAsync("versioned", 3, false, Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 3));

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(3, versions.Count);
        Assert.Equal("1", versions[0].Version);
        Assert.False(versions[0].IsCurrent);
        Assert.Null(versions[0].CreatedAt);
        Assert.Equal("3", versions[2].Version);
        Assert.True(versions[2].IsCurrent);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_SkipsMissingVersions()
    {
        _client.FindSecretAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 3));
        _client.GetSecretAsync("versioned", 1, false, Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 1));
        _client.GetSecretAsync("versioned", 2, false, Arg.Any<CancellationToken>())
            .Returns((InfisicalSecretResult?)null);
        _client.GetSecretAsync("versioned", 3, false, Arg.Any<CancellationToken>())
            .Returns(Result("versioned", "", 3));

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(2, versions.Count);
        Assert.Equal("1", versions[0].Version);
        Assert.Equal("3", versions[1].Version);
        Assert.True(versions[1].IsCurrent);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_SecretNotFound_ReturnsEmpty()
    {
        _client.FindSecretAsync("missing", Arg.Any<CancellationToken>())
            .Returns((InfisicalSecretResult?)null);

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("missing");

        Assert.Empty(versions);
        await _client.DidNotReceiveWithAnyArgs()
            .GetSecretAsync(default!, default, default, default);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.FindSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretVersionsAsync("broken"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _client.FindSecretAsync("exists", Arg.Any<CancellationToken>())
            .Returns(Result("exists", "", 1));

        await using var provider = CreateProvider();

        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _client.FindSecretAsync("missing", Arg.Any<CancellationToken>())
            .Returns((InfisicalSecretResult?)null);

        await using var provider = CreateProvider();

        Assert.False(await provider.SecretExistsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.FindSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }

    [Fact]
    public async Task DisposeAsync_DisposesClient()
    {
        var provider = CreateProvider();

        await provider.DisposeAsync();

        await _client.Received(1).DisposeAsync();
    }
}
