using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecretsManager.Builder;
using SecretsManager.Internal;

namespace SecretsManager.FileSystem.Tests;

/// <summary>
/// A key source that returns a fixed key, so the provider can be tested without a second backend.
/// </summary>
internal sealed class StaticKeySource : IEncryptionKeyProvider
{
    private readonly byte[] _key;

    public StaticKeySource(byte[] key, string keyId = "test@1") => (_key, KeyId) = (key, keyId);

    public string KeyId { get; }

    public int CurrentCalls { get; private set; }

    public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
    {
        CurrentCalls++;
        return ValueTask.FromResult(new EncryptionKeyMaterial(_key, KeyId));
    }

    public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new EncryptionKeyMaterial(_key, keyId));
}

/// <summary>
/// A key source that always throws, to prove the provider fails closed rather than falling back to
/// writing plaintext.
/// </summary>
internal sealed class ThrowingKeySource : IEncryptionKeyProvider
{
    public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default) =>
        throw new SecretProviderException("key source unavailable");

    public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
        throw new SecretProviderException("key source unavailable");
}

public sealed class FileSystemKeyProviderTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public FileSystemKeyProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecretsManagerKeyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        return Task.CompletedTask;
    }

    private string RawEnvelope(string key, string version = "v1") =>
        File.ReadAllText(Path.Combine(_tempDir, key, $"{version}.json"));

    /// <summary>
    /// Disposing a provider while an operation holds a key lock must not deadlock and must not make
    /// the operation's own Release throw. This is the regression for a drain loop that waited on
    /// <see cref="SemaphoreSlim.CurrentCount"/> — <c>Wait()</c> leaves the count at zero, so the
    /// loop never exited while a lock was held.
    /// </summary>
    [Fact]
    public async Task Dispose_DuringInFlightOperation_DoesNotDeadlockOrThrow()
    {
        // The key fetch blocks, so PutSecretAsync still holds the per-key lock when we dispose.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var provider = new SecretProviderBuilder()
            .WithFileSystem(o =>
            {
                o.BasePath = _tempDir;
                o.EncryptionKeyProvider = new GatedKeySource(_key, gate.Task, started);
            })
            .Build();

        var put = provider.PutSecretAsync("k", "value");

        // Wait until the put reaches the store lock, then dispose while it is held.
        await started.Task;

        var dispose = Task.Run(() => provider.DisposeAsync().AsTask());
        var completed = await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.True(completed == dispose, "Dispose deadlocked while a key lock was held.");

        // Release the fetch; the operation must finish rather than throw ObjectDisposedException.
        gate.SetResult(true);
        await put;
    }

    /// <summary>
    /// The test that proves the key provider object survives the builder. Without the captured-options
    /// factory the object is dropped, the provider finds no key, and the value lands on disk in
    /// cleartext — encryption silently off.
    /// </summary>
    [Fact]
    public async Task WithFileSystem_WithKeyProvider_WritesCiphertextOnDisk()
    {
        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o =>
            {
                o.BasePath = _tempDir;
                o.EncryptionKeyProvider = new StaticKeySource(_key);
            })
            .Build();

        await provider.PutSecretAsync("k", "sensitive-value");

        var raw = RawEnvelope("k");
        Assert.DoesNotContain("sensitive-value", raw);
        Assert.Contains("\"encrypted\": true", raw);
        Assert.Contains("\"keyId\": \"test@1\"", raw);

        Assert.Equal("sensitive-value", (await provider.GetSecretAsync("k")).Value);
    }

    [Fact]
    public async Task WithFileSystem_KeyProviderIsResolvedOncePerInstance()
    {
        var source = new StaticKeySource(_key);

        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = source; })
            .Build();

        await provider.PutSecretAsync("a", "1");
        await provider.PutSecretAsync("b", "2");
        await provider.GetSecretAsync("a");

        Assert.Equal(1, source.CurrentCalls);
    }

    [Fact]
    public async Task PutSecret_KeySourceThrows_FailsClosedAndWritesNothing()
    {
        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = new ThrowingKeySource(); })
            .Build();

        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("k", "sensitive-value"));

        // Nothing may be written, and above all nothing in cleartext.
        Assert.False(File.Exists(Path.Combine(_tempDir, "k", "v1.json")));
    }

    [Fact]
    public async Task KeyProvider_ReturningWrongLength_FailsClosed()
    {
        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = new StaticKeySource(new byte[16]); })
            .Build();

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("k", "value"));

        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public async Task WarmUpAsync_ResolvesTheKeyEagerly()
    {
        var source = new StaticKeySource(_key);

        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = source; })
            .Build();

        await ((FileSystemSecretProvider)provider).WarmUpAsync();

        Assert.Equal(1, source.CurrentCalls);
    }

    [Fact]
    public async Task WarmUpAsync_KeySourceThrows_SurfacesAtStartup()
    {
        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = new ThrowingKeySource(); })
            .Build();

        await Assert.ThrowsAsync<SecretProviderException>(
            () => ((FileSystemSecretProvider)provider).WarmUpAsync().AsTask());
    }

    [Fact]
    public async Task EnvelopeWithKeyIdButNoProvider_Throws()
    {
        await using (var keyedProvider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = new StaticKeySource(_key); })
            .Build())
        {
            await keyedProvider.PutSecretAsync("k", "value");
        }

        // Same static key configured, but no key provider to resolve the recorded id.
        await using var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKey = Convert.ToBase64String(_key); })
            .Build();

        var ex = await Assert.ThrowsAsync<SecretProviderException>(() => provider.GetSecretAsync("k"));
        Assert.Contains("key id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rotation end to end: put a new version of the key at the source, write again, then read
    /// <em>both</em> values back. The old value still decrypts because its envelope names the key
    /// version it was written with.
    /// </summary>
    [Fact]
    public async Task Rotation_OldAndNewValuesBothDecrypt()
    {
        // One rotating source: it can serve both key versions, and reports which is current.
        var source = new RotatingFileSystemKeySource(_key, RandomNumberGenerator.GetBytes(32));

        await using (var provider = (FileSystemSecretProvider)new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = source; })
            .Build())
        {
            // Written under version 1.
            await provider.PutSecretAsync("rotated", "first");
            Assert.Contains("\"keyId\": \"sm/data-key@1\"", RawEnvelope("rotated"));

            // Rotate: the same source now reports version 2 as current.
            source.Rotate();
            provider.InvalidateKey();
            await provider.PutSecretAsync("rotated", "second");

            // The current version reads with the new key.
            Assert.Equal("second", (await provider.GetSecretAsync("rotated")).Value);

            // The historical v1 still decrypts, by fetching key version 1 on demand.
            var historical = await provider.GetSecretAsync("rotated", new SecretQuery { Version = "v1" });
            Assert.Equal("first", historical.Value);
        }
    }

    /// <summary>A key source whose current version can be rotated, mimicking a source of versions.</summary>
    private sealed class RotatingFileSystemKeySource(byte[] first, byte[] second) : IEncryptionKeyProvider
    {
        private bool _rotated;

        public void Rotate() => _rotated = true;

        public ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_rotated
                ? new EncryptionKeyMaterial(second, "sm/data-key@2")
                : new EncryptionKeyMaterial(first, "sm/data-key@1"));

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(keyId.EndsWith("@2", StringComparison.Ordinal)
                ? new EncryptionKeyMaterial(second, keyId)
                : new EncryptionKeyMaterial(first, keyId));
    }

    /// <summary>
    /// Ownership: the key source wraps a provider the caller owns, so disposing the encrypted
    /// provider must not take the wrapped provider down with it.
    /// </summary>
    [Fact]
    public async Task KeySource_DoesNotDisposeTheWrappedProvider()
    {
        var wrapped = new TrackingProvider();
        wrapped.Seed("sm/data-key", Convert.ToBase64String(_key), version: "1");

        var source = new SecretProviderKeySource(wrapped, "sm/data-key");

        await using (var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = source; })
            .Build())
        {
            await provider.PutSecretAsync("k", "value");
        }

        // The encrypted provider must not take the caller-owned provider down with it.
        Assert.False(wrapped.Disposed);

        // And the source is still usable on its own.
        var material = await source.GetCurrentKeyAsync();
        Assert.Equal("sm/data-key@1", material.KeyId);
    }

    /// <summary>
    /// Disposing while a key fetch is in flight is safe: the resolver does not take its lock
    /// synchronously, so <c>DisposeAsync</c> cannot block behind the fetch, and the fetch is not
    /// left running against a disposed resolver.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_DuringInFlightKeyFetch_DoesNotThrow()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new GatedKeySource(_key, gate.Task, started);

        var provider = new SecretProviderBuilder()
            .WithFileSystem(o => { o.BasePath = _tempDir; o.EncryptionKeyProvider = source; })
            .Build();

        var put = provider.PutSecretAsync("k", "value");

        // Wait until the fetch reaches the resolver, then dispose while it is still pending.
        await started.Task;
        await provider.DisposeAsync();

        // Release the fetch. A correct implementation neither blocks nor throws here.
        gate.SetResult(true);
        await put;
    }

    /// <summary>
    /// A key source that blocks until a gate opens, to hold a fetch open across a dispose.
    /// Honours the token so it behaves like a real network-backed source.
    /// </summary>
    private sealed class GatedKeySource(
        byte[] key, Task gate, TaskCompletionSource<bool>? started = null) : IEncryptionKeyProvider
    {
        public async ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
        {
            started?.TrySetResult(true);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new EncryptionKeyMaterial(key, "gated@1");
        }

        public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EncryptionKeyMaterial(key, keyId));
    }

    /// <summary>An in-memory provider that records whether it was disposed.</summary>
    private sealed class TrackingProvider : ISecretProvider
    {
        private readonly Dictionary<string, SecretValue> _secrets = new(StringComparer.Ordinal);

        public bool Disposed { get; private set; }

        public void Seed(string key, string value, string? version = null) =>
            _secrets[key] = new SecretValue { Key = key, Value = value, Version = version };

        public Task<SecretValue> GetSecretAsync(
            string key, SecretQuery? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.TryGetValue(key, out var value)
                ? value
                : throw new SecretProviderException($"Secret '{key}' not found."));

        public Task<SecretValue> PutSecretAsync(
            string key, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecretValue { Key = key, Value = value });

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SecretVersionInfo>>(Array.Empty<SecretVersionInfo>());

        public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.ContainsKey(key));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class FileSystemOptionsKeyValidationTests
{
    private static FileSystemOptions Options(Action<FileSystemOptions> configure)
    {
        var options = new FileSystemOptions { BasePath = "/var/secrets" };
        configure(options);
        return options;
    }

    [Fact]
    public void Validate_AcceptsKeyProviderAlone()
    {
        var options = Options(o => o.EncryptionKeyProvider = new StaticKeySource(new byte[32]));

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_RejectsKeyProviderWithStaticKey()
    {
        var options = Options(o =>
        {
            o.EncryptionKeyProvider = new StaticKeySource(new byte[32]);
            o.EncryptionKey = Convert.ToBase64String(new byte[32]);
        });

        Assert.Contains("mutually exclusive", Assert.Single(options.Validate()));
    }
}
