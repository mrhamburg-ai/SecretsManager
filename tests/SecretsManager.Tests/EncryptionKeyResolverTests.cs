using System.Security.Cryptography;
using System.Text;
using SecretsManager.Internal;

namespace SecretsManager.Tests;

public sealed class EncryptionKeyResolverTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private static readonly string KeyBase64 = Convert.ToBase64String(Key);

    private sealed class CountingSource : IEncryptionKeyProvider
    {
        private readonly byte[] _key;
        public int Calls { get; private set; }

        public CountingSource(byte[] key) => _key = key;

        public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(new EncryptionKeyMaterial(_key, "k@1"));
        }

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(new EncryptionKeyMaterial(_key, keyId));
        }
    }

    [Fact]
    public async Task NoKeyConfigured_IsDisabled()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, null, null);

        Assert.False(resolver.IsEnabled);
    }

    [Fact]
    public async Task NoKeyConfigured_ReturnsNullEncryptor()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, null, null);

        var (encryptor, keyId) = await resolver.GetCurrentAsync();

        Assert.IsType<NullEncryptor>(encryptor);
        Assert.Null(keyId);
    }

    [Fact]
    public async Task StaticKey_IsEnabledAndHasNoKeyId()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null, null);

        Assert.True(resolver.IsEnabled);

        var (encryptor, keyId) = await resolver.GetCurrentAsync();

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Null(keyId);
    }

    [Fact]
    public async Task KeyFile_IsEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"key-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(path, Key);

        try
        {
            await using var resolver = new EncryptionKeyResolver(null, path, null, null);
            var (encryptor, _) = await resolver.GetCurrentAsync();

            Assert.IsType<AesGcmEncryptor>(encryptor);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task KeyProvider_IsEnabledAndReturnsItsKeyId()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, new CountingSource(Key), null);

        var (encryptor, keyId) = await resolver.GetCurrentAsync();

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Equal("k@1", keyId);
    }

    [Fact]
    public async Task KeyProvider_IsFetchedOnceAndCached()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source, null);

        await resolver.GetCurrentAsync();
        await resolver.GetCurrentAsync();
        await resolver.GetCurrentAsync();

        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task InvalidateKey_ForcesRefetch()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source, null);

        await resolver.GetCurrentAsync();
        resolver.InvalidateKey();
        await resolver.GetCurrentAsync();

        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task InvalidateKey_KeepsHistoricalKeys()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source, null);

        await resolver.GetCurrentAsync();
        await resolver.GetByKeyIdAsync("k@1");
        resolver.InvalidateKey();
        await resolver.GetCurrentAsync();

        // The historical key is still cached, so only the current key was re-fetched.
        await resolver.GetByKeyIdAsync("k@1");
        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task FailedFirstFetch_IsRetried()
    {
        var source = new FailingThenSucceedingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source, null);

        await Assert.ThrowsAsync<SecretProviderException>(() => resolver.GetCurrentAsync().AsTask());
        var (encryptor, _) = await resolver.GetCurrentAsync();

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task GetByKeyId_NullKeyId_UsesLegacyKeyWhenSet()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, null, KeyBase64);

        var encryptor = await resolver.GetByKeyIdAsync(null);

        Assert.IsType<AesGcmEncryptor>(encryptor);
    }

    [Fact]
    public async Task GetByKeyId_NullKeyId_FallsBackToStaticKey()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null, null);

        var encryptor = await resolver.GetByKeyIdAsync(null);

        Assert.IsType<AesGcmEncryptor>(encryptor);
    }

    [Fact]
    public async Task GetByKeyId_NullKeyId_NoKeyAtAll_Throws()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, null, null);

        await Assert.ThrowsAsync<SecretProviderException>(() => resolver.GetByKeyIdAsync(null).AsTask());
    }

    [Fact]
    public async Task GetByKeyId_WithKeyIdButNoProvider_Throws()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null, null);

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => resolver.GetByKeyIdAsync("k@1").AsTask());

        Assert.Contains("EncryptionKeyProvider", ex.Message);
    }

    [Fact]
    public async Task GetByKeyId_ResolvesThroughProvider()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source, null);

        var encryptor = await resolver.GetByKeyIdAsync("k@1");

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task ResolvedKeyOfWrongLength_Throws()
    {
        await using var resolver = new EncryptionKeyResolver(Convert.ToBase64String(new byte[16]), null, null, null);

        var ex = await Assert.ThrowsAsync<SecretProviderException>(() => resolver.GetCurrentAsync().AsTask());

        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public async Task EncryptorRoundTrips()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null, null);

        var (encryptor, _) = await resolver.GetCurrentAsync();
        var payload = encryptor.Encrypt(Encoding.UTF8.GetBytes("hello"));

        Assert.Equal("hello", Encoding.UTF8.GetString(encryptor.Decrypt(payload)));
    }

    private sealed class FailingThenSucceedingSource : IEncryptionKeyProvider
    {
        private readonly byte[] _key;
        public int Calls { get; private set; }

        public FailingThenSucceedingSource(byte[] key) => _key = key;

        public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls == 1)
                throw new SecretProviderException("transient failure");

            return ValueTask.FromResult(new EncryptionKeyMaterial(_key, "k@1"));
        }

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EncryptionKeyMaterial(_key, keyId));
    }
}
