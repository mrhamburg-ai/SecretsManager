using System.Text;
using SecretsManager.Internal;

namespace SecretsManager.Tests;

/// <summary>
/// A minimal in-memory <see cref="ISecretProvider"/> used to back a <see cref="SecretProviderKeySource"/>.
/// </summary>
internal sealed class FakeSecretProvider : ISecretProvider
{
    private readonly Dictionary<string, SecretValue> _secrets = new(StringComparer.Ordinal);

    public int GetCalls { get; private set; }

    public void Seed(string key, string value, string? version = null) =>
        _secrets[key] = new SecretValue { Key = key, Value = value, Version = version };

    public Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        GetCalls++;

        if (!_secrets.TryGetValue(key, out var value))
            throw new SecretProviderException($"Secret '{key}' not found.");

        if (query?.Version is { } version && value.Version != version)
            throw new SecretProviderException($"Version '{version}' of secret '{key}' not found.");

        return Task.FromResult(value);
    }

    public Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SecretValue { Key = key, Value = value });

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SecretVersionInfo>>(Array.Empty<SecretVersionInfo>());

    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.ContainsKey(key));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class SecretProviderKeySourceTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task GetCurrentKey_DecodesBase64AndDerivesKeyIdFromVersion()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("sm/data-key", Convert.ToBase64String(Key), version: "3");

        var source = new SecretProviderKeySource(provider, "sm/data-key");
        var material = await source.GetCurrentKeyAsync();

        Assert.Equal(Key, material.Key);
        Assert.Equal("sm/data-key@3", material.KeyId);
    }

    [Fact]
    public async Task GetCurrentKey_WithoutVersion_Throws()
    {
        // A source that reports no version cannot safely be used as a key source: the key id is
        // derived from the version, so a missing version would make the id stable across a
        // rotation and leave existing values unreadable.
        var provider = new FakeSecretProvider();
        provider.Seed("sm/data-key", Convert.ToBase64String(Key));

        var source = new SecretProviderKeySource(provider, "sm/data-key");

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => source.GetCurrentKeyAsync().AsTask());

        Assert.Contains("no version", ex.Message);
    }

    [Fact]
    public async Task GetCurrentKey_Utf8Encoding_UsesRawBytes()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("k", new string('a', 32), version: "1");

        var source = new SecretProviderKeySource(provider, "k", SecretKeyEncoding.Utf8);
        var material = await source.GetCurrentKeyAsync();

        Assert.Equal(Encoding.UTF8.GetBytes(new string('a', 32)), material.Key);
        Assert.Equal("k@1", material.KeyId);
    }

    [Fact]
    public async Task GetKeyAsync_FetchesTheRecordedVersion()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("sm/data-key", Convert.ToBase64String(Key), version: "7");

        var source = new SecretProviderKeySource(provider, "sm/data-key");
        var material = await source.GetKeyAsync("sm/data-key@7");

        Assert.Equal(Key, material.Key);
        Assert.Equal("sm/data-key@7", material.KeyId);
    }

    [Fact]
    public async Task GetKeyAsync_UnknownVersion_Throws()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("sm/data-key", Convert.ToBase64String(Key), version: "1");

        var source = new SecretProviderKeySource(provider, "sm/data-key");

        await Assert.ThrowsAsync<SecretProviderException>(() => source.GetKeyAsync("sm/data-key@99").AsTask());
    }

    [Fact]
    public async Task GetKeyAsync_ForeignKeyId_Throws()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("sm/data-key", Convert.ToBase64String(Key), version: "1");

        var source = new SecretProviderKeySource(provider, "sm/data-key");

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => source.GetKeyAsync("other-key@1").AsTask());

        Assert.Contains("does not belong", ex.Message);
    }

    [Fact]
    public async Task KeyOfWrongLength_Throws()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("k", Convert.ToBase64String(new byte[16]));

        var source = new SecretProviderKeySource(provider, "k");

        var ex = await Assert.ThrowsAsync<SecretProviderException>(() => source.GetCurrentKeyAsync().AsTask());

        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public async Task InvalidBase64_ThrowsSecretProviderException()
    {
        var provider = new FakeSecretProvider();
        provider.Seed("k", "not base64 at all", version: "1");

        var source = new SecretProviderKeySource(provider, "k");

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => source.GetCurrentKeyAsync().AsTask());

        Assert.Contains("not valid base64", ex.Message);
    }

    [Fact]
    public void Constructor_NullProvider_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SecretProviderKeySource(null!, "k"));

    [Fact]
    public void Constructor_NullKey_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SecretProviderKeySource(new FakeSecretProvider(), null!));
}
