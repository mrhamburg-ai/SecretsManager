using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NSubstitute;
using SecretsManager.Internal;
using SecretsManager.PostgreSql.Internal;

namespace SecretsManager.PostgreSql.Tests;

/// <summary>
/// Returns a fixed key for the current version and, on demand, a specific historical version.
/// </summary>
internal sealed class RotatableKeySource : IEncryptionKeyProvider
{
    private readonly IReadOnlyDictionary<string, byte[]> _keys;
    private readonly string _current;

    public RotatableKeySource(IReadOnlyDictionary<string, byte[]> keys, string current) =>
        (_keys, _current) = (keys, current);

    public int CurrentCalls { get; private set; }

    public int VersionCalls { get; private set; }

    public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
    {
        CurrentCalls++;
        return ValueTask.FromResult(new EncryptionKeyMaterial(_keys[_current], _current));
    }

    public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        VersionCalls++;
        return ValueTask.FromResult(new EncryptionKeyMaterial(_keys[keyId], keyId));
    }
}

public sealed class PostgreSqlKeyProviderTests
{
    private readonly IPostgreSqlSecretStore _mockStore = Substitute.For<IPostgreSqlSecretStore>();
    private readonly byte[] _keyV1 = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _keyV2 = RandomNumberGenerator.GetBytes(32);
    private const string V1 = "sm/data-key@v1";
    private const string V2 = "sm/data-key@v2";

    private PostgreSqlSecretRow MakeSecret(int id, string key) => new(
        id, key, "text/plain", null,
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        DateTimeOffset.Parse("2026-01-02T00:00:00Z"));

    private PostgreSqlVersionRow MakeVersion(int id, string value, bool encrypted, int version = 1) => new(
        version, id, version, value, encrypted,
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"), true);

    private static string EncryptedPayloadFor(byte[] key, string plaintext, string? keyId, string secretKey = "s")
    {
        using var encryptor = new AesGcmEncryptor(key);
        // The provider authenticates "{keyId}|{secretKey}" as associated data, so the payload must
        // be built the same way or decryption fails the tag check.
        var associatedData = Encoding.UTF8.GetBytes($"{keyId}|{secretKey}");
        var payload = encryptor.Encrypt(Encoding.UTF8.GetBytes(plaintext), associatedData);

        return JsonSerializer.Serialize(new
        {
            n = Convert.ToBase64String(payload.Nonce),
            c = Convert.ToBase64String(payload.Ciphertext),
            t = Convert.ToBase64String(payload.Tag),
            k = keyId
        });
    }

    private void StubPut() =>
        _mockStore.PutSecretAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns((MakeSecret(1, "s"), MakeVersion(1, "v", true)));

    private PostgreSqlSecretProvider CreateWithStaticKey(byte[] key) =>
        new(_mockStore, new EncryptionKeyResolver(Convert.ToBase64String(key), null, null, null));

    private PostgreSqlSecretProvider CreateWithRotatableSource(RotatableKeySource source) =>
        new(_mockStore, new EncryptionKeyResolver(null, null, source, null));

    [Fact]
    public async Task KeyProvider_IsFetchedOncePerInstance()
    {
        var source = new RotatableKeySource(new Dictionary<string, byte[]>
        {
            [V1] = _keyV1
        }, V1);

        _mockStore.GetSecretByKeyAsync("s", Arg.Any<CancellationToken>()).Returns(MakeSecret(1, "s"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(1, EncryptedPayloadFor(_keyV1, "under-v1", V1), encrypted: true));
        StubPut();

        await using var provider = CreateWithRotatableSource(source);

        await provider.PutSecretAsync("s", "a");
        await provider.PutSecretAsync("s", "b");
        await provider.GetSecretAsync("s");

        Assert.Equal(1, source.CurrentCalls);
    }

    /// <summary>
    /// Migration: a row written before key ids existed has no <c>k</c> field. It must still decrypt
    /// using the legacy key, and new writes must record the key id.
    /// </summary>
    [Fact]
    public async Task LegacyKey_DecryptsRowsWithNoKeyId_ThenRecordsKeyId()
    {
        var legacyPayload = EncryptedPayloadFor(_keyV1, "legacy-value", null);

        _mockStore.GetSecretByKeyAsync("s", Arg.Any<CancellationToken>()).Returns(MakeSecret(1, "s"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(1, legacyPayload, encrypted: true));
        StubPut();

        // Migrated to a key provider, with the old key kept as a decrypt-only fallback.
        var source = new RotatableKeySource(new Dictionary<string, byte[]> { [V2] = _keyV2 }, V2);
        await using (var migrated = new PostgreSqlSecretProvider(
                   _mockStore,
                   new EncryptionKeyResolver(null, null, source, Convert.ToBase64String(_keyV1))))
        {
            // The row has no key id, so it is read with the legacy key.
            var read = await migrated.GetSecretAsync("s");
            Assert.Equal("legacy-value", read.Value);

            // New writes use the key provider and record its key id.
            await migrated.PutSecretAsync("s", "new-value");

            var written = LastWrittenValue();
            using var doc = JsonDocument.Parse(written);
            Assert.True(doc.RootElement.GetProperty("k").GetString() == V2, written);
            Assert.DoesNotContain("new-value", written);
            Assert.Equal(1, source.CurrentCalls);
        }
    }

    /// <summary>
    /// Rotation: a row written under v1 must still decrypt by fetching v1 on demand, even though the
    /// current key is now v2.
    /// </summary>
    [Fact]
    public async Task Rotation_RowWrittenUnderOldVersionStillDecrypts()
    {
        var source = new RotatableKeySource(new Dictionary<string, byte[]>
        {
            [V1] = _keyV1,
            [V2] = _keyV2
        }, V2);

        _mockStore.GetSecretByKeyAsync("s", Arg.Any<CancellationToken>()).Returns(MakeSecret(1, "s"));

        await using var provider = CreateWithRotatableSource(source);

        // The stored row records v1, so it must be fetched on demand under the current key v2.
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(1, EncryptedPayloadFor(_keyV1, "under-v1", V1), encrypted: true));

        var read = await provider.GetSecretAsync("s");

        Assert.Equal("under-v1", read.Value);
        Assert.Equal(1, source.VersionCalls);
        Assert.Equal(0, source.CurrentCalls);
    }

    private string LastWrittenValue() =>
        _mockStore.ReceivedCalls()
            .Select(c => c.GetMethodInfo().Name == nameof(IPostgreSqlSecretStore.PutSecretAsync)
                ? c.GetArguments()[1] as string
                : null)
            .OfType<string>()
            .Last();

    [Fact]
    public async Task KeySourceThrows_FailsClosed()
    {
        await using var provider = new PostgreSqlSecretProvider(
            _mockStore, new EncryptionKeyResolver(null, null, new ThrowingSource(), null));

        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("s", "value"));
    }

    /// <summary>
    /// <c>WarmUpAsync</c> promises to ensure the schema when <c>AutoCreateSchema</c> is set, so it
    /// must do so — otherwise a missing schema surfaces on the first read instead of at startup.
    /// </summary>
    [Fact]
    public async Task WarmUpAsync_WithAutoCreateSchema_EnsuresTheSchema()
    {
        var source = new RotatableKeySource(new Dictionary<string, byte[]> { [V1] = _keyV1 }, V1);
        await using var provider = new PostgreSqlSecretProvider(
            _mockStore, new EncryptionKeyResolver(null, null, source, null), autoCreateSchema: true);

        await provider.WarmUpAsync();

        await _mockStore.Received(1).EnsureSchemaAsync(Arg.Any<CancellationToken>());
        Assert.Equal(1, source.CurrentCalls);
    }

    [Fact]
    public async Task WarmUpAsync_WithoutAutoCreateSchema_DoesNotTouchTheSchema()
    {
        var source = new RotatableKeySource(new Dictionary<string, byte[]> { [V1] = _keyV1 }, V1);
        await using var provider = new PostgreSqlSecretProvider(
            _mockStore, new EncryptionKeyResolver(null, null, source, null));

        await provider.WarmUpAsync();

        await _mockStore.DidNotReceive().EnsureSchemaAsync(Arg.Any<CancellationToken>());
    }

    private sealed class ThrowingSource : IEncryptionKeyProvider
    {
        public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default) =>
            throw new SecretProviderException("key source unavailable");

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            throw new SecretProviderException("key source unavailable");
    }
}
