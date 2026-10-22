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
        await using var resolver = new EncryptionKeyResolver(null, null, null);

        Assert.False(resolver.IsEnabled);
    }

    [Fact]
    public async Task NoKeyConfigured_ReturnsNullEncryptor()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, null);

        var (encryptor, keyId) = await resolver.GetCurrentAsync();

        Assert.IsType<NullEncryptor>(encryptor);
        Assert.Null(keyId);
    }

    [Fact]
    public async Task StaticKey_IsEnabledAndHasNoKeyId()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null);

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
            await using var resolver = new EncryptionKeyResolver(null, path, null);
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
        await using var resolver = new EncryptionKeyResolver(null, null, new CountingSource(Key));

        var (encryptor, keyId) = await resolver.GetCurrentAsync();

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Equal("k@1", keyId);
    }

    [Fact]
    public async Task KeyProvider_IsFetchedOnceAndCached()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        await resolver.GetCurrentAsync();
        await resolver.GetCurrentAsync();
        await resolver.GetCurrentAsync();

        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task InvalidateKey_ForcesRefetch()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        await resolver.GetCurrentAsync();
        resolver.InvalidateKey();
        await resolver.GetCurrentAsync();

        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task InvalidateKey_KeepsHistoricalKeys()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source);

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
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        await Assert.ThrowsAsync<SecretProviderException>(() => resolver.GetCurrentAsync().AsTask());
        var (encryptor, _) = await resolver.GetCurrentAsync();

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task GetByKeyId_NullKeyId_FallsBackToStaticKey()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null);

        var encryptor = await resolver.GetByKeyIdAsync(null);

        Assert.IsType<AesGcmEncryptor>(encryptor);
    }

    [Fact]
    public async Task GetByKeyId_NullKeyId_NoKeyAtAll_Throws()
    {
        await using var resolver = new EncryptionKeyResolver(null, null, null);

        await Assert.ThrowsAsync<SecretProviderException>(() => resolver.GetByKeyIdAsync(null).AsTask());
    }

    [Fact]
    public async Task GetByKeyId_WithKeyIdButNoProvider_Throws()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null);

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => resolver.GetByKeyIdAsync("k@1").AsTask());

        Assert.Contains("EncryptionKeyProvider", ex.Message);
    }

    [Fact]
    public async Task GetByKeyId_ResolvesThroughProvider()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        var encryptor = await resolver.GetByKeyIdAsync("k@1");

        Assert.IsType<AesGcmEncryptor>(encryptor);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task ResolvedKeyOfWrongLength_Throws()
    {
        await using var resolver = new EncryptionKeyResolver(Convert.ToBase64String(new byte[16]), null, null);

        var ex = await Assert.ThrowsAsync<SecretProviderException>(() => resolver.GetCurrentAsync().AsTask());

        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public async Task EncryptorRoundTrips()
    {
        await using var resolver = new EncryptionKeyResolver(KeyBase64, null, null);

        var (encryptor, _) = await resolver.GetCurrentAsync();
        var payload = encryptor.Encrypt(Encoding.UTF8.GetBytes("hello"));

        Assert.Equal("hello", Encoding.UTF8.GetString(encryptor.Decrypt(payload)));
    }

    [Fact]
    public async Task ResolvedKeyIdForDifferentBytes_Throws()
    {
        var first = new byte[32];
        var second = new byte[32];
        second[0] = 1;

        var source = new SwappingKeySource(first, second);
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        // First write resolves the key id and caches it.
        var (encryptor, keyId) = await resolver.GetCurrentAsync();
        Assert.Equal("k@1", keyId);
        Assert.NotNull(encryptor);

        // Rotate at the source: the same key id now refers to different bytes — the shape a source
        // with a constant version produces (Passbolt always reports "1").
        source.Swap();
        resolver.InvalidateKey();

        // Returning the stale encryptor here would make every value written under this id
        // permanently unreadable, with no error at the point of rotation, so this must throw.
        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => resolver.GetCurrentAsync().AsTask());

        Assert.Contains("different key", ex.Message);
    }

    /// <summary>
    /// Serves <c>first</c> until <see cref="Swap"/> is called, then serves <c>second</c>, while
    /// always reporting the same key id.
    /// </summary>
    private sealed class SwappingKeySource(byte[] first, byte[] second) : IEncryptionKeyProvider
    {
        private bool _swapped;

        public void Swap() => _swapped = true;

        private byte[] Current => _swapped ? second : first;

        public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EncryptionKeyMaterial(Current, "k@1"));

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EncryptionKeyMaterial(Current, keyId));
    }

    [Fact]
    public async Task GetByKeyId_ConcurrentReads_DoNotThrow()
    {
        var source = new RotatingSource();
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < 50; i++)
                    await resolver.GetByKeyIdAsync("k@" + (i % 4 + 1));
            }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        await using var resolver = new EncryptionKeyResolver(
            Convert.ToBase64String(Key), null, null);

        await resolver.DisposeAsync();
        await resolver.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_DoesNotThrow_WhenAFetchIsInFlight()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var resolver = new EncryptionKeyResolver(null, null, new GatedSource(gate.Task, started));

        var fetch = resolver.GetCurrentAsync();
        await started.Task;

        resolver.Dispose();
        gate.SetResult(true);

        await fetch;
    }

    [Fact]
    public async Task InvalidateKey_DuringConcurrentPuts_DoesNotThrow()
    {
        var source = new CountingSource(Key);
        await using var resolver = new EncryptionKeyResolver(null, null, source);

        var puts = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < 20; i++)
                {
                    await resolver.GetCurrentAsync();
                    resolver.InvalidateKey();
                }
            }))
            .ToArray();

        await Task.WhenAll(puts);
    }

    private sealed class RotatingSource : IEncryptionKeyProvider
    {
        private readonly byte[][] _keys = new byte[4][];

        public RotatingSource()
        {
            for (var i = 0; i < _keys.Length; i++)
                _keys[i] = new byte[32];
        }

        public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EncryptionKeyMaterial(_keys[0], "k@1"));

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            var n = int.Parse(keyId[(keyId.IndexOf('@') + 1)..]);
            return ValueTask.FromResult(new EncryptionKeyMaterial(_keys[(n - 1) % _keys.Length], keyId));
        }
    }

    private sealed class GatedSource(
        Task gate, TaskCompletionSource<bool>? started = null) : IEncryptionKeyProvider
    {
        public async ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
        {
            started?.TrySetResult(true);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new EncryptionKeyMaterial(new byte[32], "g@1");
        }

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EncryptionKeyMaterial(new byte[32], keyId));
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
