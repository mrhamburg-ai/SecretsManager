using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Doppler.Internal;

namespace SecretsManager.Doppler.Tests;

public sealed class DopplerSecretProviderTests
{
    private const string ServiceToken = "dp.st.dev.abcdefghijklmnopqrstuvwxyz0123456789";
    private const string PersonalToken = "dp.pt.abcdefghijklmnopqrstuvwxyz0123456789";

    private readonly IDopplerApiClient _client = Substitute.For<IDopplerApiClient>();

    private DopplerSecretProvider CreateProvider() => new(_client);

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DopplerSecretProvider((DopplerOptions)null!));
    }

    [Fact]
    public void Constructor_MissingToken_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DopplerSecretProvider(new DopplerOptions()));

        Assert.Contains("Token is required", exception.Message);
    }

    [Fact]
    public void Constructor_PersonalTokenWithoutProjectAndConfig_Throws()
    {
        var options = new DopplerOptions
        {
            Token = PersonalToken
        };

        var exception = Assert.Throws<ArgumentException>(
            () => new DopplerSecretProvider(options));

        Assert.Contains("Project is required", exception.Message);
        Assert.Contains("Config is required", exception.Message);
    }

    [Fact]
    public void Constructor_ServiceTokenWithoutProjectAndConfig_DoesNotThrow()
    {
        var options = new DopplerOptions
        {
            Token = ServiceToken
        };

        var provider = new DopplerSecretProvider(options);

        Assert.NotNull(provider);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsValueWithoutVersion()
    {
        _client.GetSecretAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(new DopplerSecretResult("secret-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Null(result.Version);
        Assert.Null(result.Tags);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_ThrowsAndDoesNotCallClient()
    {
        await using var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("my-secret", new SecretQuery { Version = "1" }));

        Assert.Contains("does not expose per-secret versions", exception.Message);
        await _client.DidNotReceiveWithAnyArgs()
            .GetSecretAsync(default!, default);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _client.GetSecretAsync("db-config", Arg.Any<CancellationToken>())
            .Returns(new DopplerSecretResult("""{"db":{"host":"db.example.com"}}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync(
            "db-config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _client.GetSecretAsync("missing", Arg.Any<CancellationToken>())
            .Returns((DopplerSecretResult?)null);

        await using var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", exception.Key);
    }

    [Fact]
    public async Task GetSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.GetSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));

        Assert.Contains("Doppler error for secret 'broken'", exception.Message);
    }

    [Fact]
    public async Task PutSecretAsync_ReturnsStoredValue()
    {
        _client.PutSecretAsync("my-secret", "new-value", Arg.Any<CancellationToken>())
            .Returns(new DopplerSecretResult("new-value"));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("my-secret", "new-value");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Null(result.Version);
    }

    [Fact]
    public async Task PutSecretAsync_IgnoresUnsupportedMetadata()
    {
        _client.PutSecretAsync("tagged", "value", Arg.Any<CancellationToken>())
            .Returns(new DopplerSecretResult("value"));

        await using var provider = CreateProvider();
        var metadata = new SecretMetadata
        {
            ContentType = "application/json",
            Tags = new Dictionary<string, string> { ["env"] = "dev" }
        };

        var result = await provider.PutSecretAsync("tagged", "value", metadata);

        Assert.Null(result.Tags);
        await _client.Received(1)
            .PutSecretAsync("tagged", "value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.PutSecretAsync("broken", "value", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("broken", "value"));

        Assert.Contains("Doppler error putting secret 'broken'", exception.Message);
    }

    [Fact]
    public async Task DeleteSecretAsync_Delegates()
    {
        _client.DeleteSecretAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("my-secret");

        await _client.Received(1).DeleteSecretAsync("my-secret", Arg.Any<CancellationToken>());
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

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.DeleteSecretAsync("broken"));

        Assert.Contains("Doppler error deleting secret 'broken'", exception.Message);
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsClientResult()
    {
        _client.SecretExistsAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();

        Assert.True(await provider.SecretExistsAsync("my-secret"));
    }

    [Fact]
    public async Task SecretExistsAsync_ApiError_ThrowsSecretProviderException()
    {
        _client.SecretExistsAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await using var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));

        Assert.Contains("Doppler error checking existence of 'broken'", exception.Message);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsSingleCurrentVersion()
    {
        _client.SecretExistsAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("my-secret");

        var version = Assert.Single(versions);
        Assert.Equal("1", version.Version);
        Assert.True(version.IsCurrent);
        Assert.Null(version.CreatedAt);
    }

    [Fact]
    public async Task GetSecretVersionsAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _client.SecretExistsAsync("missing", Arg.Any<CancellationToken>())
            .Returns(false);

        await using var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync("missing"));

        Assert.Equal("missing", exception.Key);
    }

    [Fact]
    public async Task DisposeAsync_DisposesClient()
    {
        await using var provider = CreateProvider();

        await provider.DisposeAsync();

        await _client.Received(1).DisposeAsync();
    }
}
