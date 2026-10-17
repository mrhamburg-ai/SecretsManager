using System.Security.Cryptography;
using System.Text.Json;

namespace SecretsManager.FileSystem.Tests;

public sealed class FileSystemSecretProviderTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _encryptionKey;

    public FileSystemSecretProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecretsManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        _encryptionKey = Convert.ToBase64String(keyBytes);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        return Task.CompletedTask;
    }

    private FileSystemSecretProvider CreateProvider(bool encrypt = false) =>
        new(new FileSystemOptions
        {
            BasePath = _tempDir,
            EncryptionKey = encrypt ? _encryptionKey : null
        });

    [Fact]
    public async Task PutAndGet_RoundTrips()
    {
        await using var provider = CreateProvider();

        var result = await provider.PutSecretAsync("my-secret", "my-value");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("my-value", result.Value);
        Assert.Equal("v1", result.Version);
        Assert.NotNull(result.CreatedAt);

        var retrieved = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-value", retrieved.Value);
        Assert.Equal("v1", retrieved.Version);
    }

    [Fact]
    public async Task PutAndGet_WithEncryption_RoundTrips()
    {
        await using var provider = CreateProvider(encrypt: true);

        await provider.PutSecretAsync("encrypted-secret", "sensitive-data");
        var retrieved = await provider.GetSecretAsync("encrypted-secret");

        Assert.Equal("sensitive-data", retrieved.Value);
    }

    [Fact]
    public async Task PutWithEncryption_StoresEncryptedOnDisk()
    {
        await using var provider = CreateProvider(encrypt: true);

        await provider.PutSecretAsync("encrypted-secret", "sensitive-data");

        // Read the raw file and verify the value is not stored in plaintext
        var versionFile = Path.Combine(_tempDir, "encrypted-secret", "v1.json");
        var rawJson = await File.ReadAllTextAsync(versionFile);

        Assert.DoesNotContain("sensitive-data", rawJson);
        Assert.Contains("\"encrypted\": true", rawJson);
        Assert.Contains("\"ciphertext\":", rawJson);
    }

    [Fact]
    public async Task Versioning_CreatesMultipleVersions()
    {
        await using var provider = CreateProvider();

        await provider.PutSecretAsync("versioned", "value-1");
        await provider.PutSecretAsync("versioned", "value-2");
        await provider.PutSecretAsync("versioned", "value-3");

        var versions = await provider.GetSecretVersionsAsync("versioned");

        Assert.Equal(3, versions.Count);
        Assert.Equal("v1", versions[0].Version);
        Assert.Equal("v2", versions[1].Version);
        Assert.Equal("v3", versions[2].Version);
        Assert.True(versions[2].IsCurrent);
        Assert.False(versions[0].IsCurrent);
    }

    [Fact]
    public async Task GetSpecificVersion_ReturnsCorrectValue()
    {
        await using var provider = CreateProvider();

        await provider.PutSecretAsync("versioned", "old-value");
        await provider.PutSecretAsync("versioned", "new-value");

        var v1 = await provider.GetSecretAsync("versioned", new SecretQuery { Version = "v1" });
        var latest = await provider.GetSecretAsync("versioned");

        Assert.Equal("old-value", v1.Value);
        Assert.Equal("new-value", latest.Value);
    }

    [Fact]
    public async Task JsonPropertyExtraction_ExtractsNestedProperty()
    {
        await using var provider = CreateProvider();

        var json = """{"database":{"host":"localhost","port":5432}}""";
        await provider.PutSecretAsync("config", json, new SecretMetadata { ContentType = "application/json" });

        var host = await provider.GetSecretAsync("config", new SecretQuery { Property = "database.host" });
        var port = await provider.GetSecretAsync("config", new SecretQuery { Property = "database.port" });

        Assert.Equal("localhost", host.Value);
        Assert.Equal("5432", port.Value);
    }

    [Fact]
    public async Task DeleteSecret_RemovesSecret()
    {
        await using var provider = CreateProvider();

        await provider.PutSecretAsync("to-delete", "value");
        Assert.True(await provider.SecretExistsAsync("to-delete"));

        await provider.DeleteSecretAsync("to-delete");
        Assert.False(await provider.SecretExistsAsync("to-delete"));
    }

    [Fact]
    public async Task DeleteSecret_ThrowsWhenNotFound()
    {
        await using var provider = CreateProvider();

        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("nonexistent"));
    }

    [Fact]
    public async Task GetSecret_ThrowsWhenNotFound()
    {
        await using var provider = CreateProvider();

        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("nonexistent"));

        Assert.Equal("nonexistent", ex.Key);
    }

    [Fact]
    public async Task SecretExists_ReturnsFalseForNonexistent()
    {
        await using var provider = CreateProvider();

        Assert.False(await provider.SecretExistsAsync("nope"));
    }

    [Fact]
    public async Task TagsAndMetadata_RoundTrip()
    {
        await using var provider = CreateProvider();

        var tags = new Dictionary<string, string> { ["env"] = "prod", ["team"] = "platform" };
        var metadata = new SecretMetadata
        {
            ContentType = "application/json",
            Tags = tags
        };

        await provider.PutSecretAsync("tagged", "value", metadata);
        var retrieved = await provider.GetSecretAsync("tagged");

        Assert.Equal("application/json", retrieved.ContentType);
        Assert.NotNull(retrieved.Tags);
        Assert.Equal("prod", retrieved.Tags["env"]);
        Assert.Equal("platform", retrieved.Tags["team"]);
    }

    [Fact]
    public async Task ConcurrentPuts_DoNotCorrupt()
    {
        await using var provider = CreateProvider();

        var tasks = Enumerable.Range(1, 10)
            .Select(i => provider.PutSecretAsync("concurrent", $"value-{i}"))
            .ToArray();

        await Task.WhenAll(tasks);

        var versions = await provider.GetSecretVersionsAsync("concurrent");
        Assert.Equal(10, versions.Count);

        // Current version should be retrievable
        var current = await provider.GetSecretAsync("concurrent");
        Assert.NotNull(current.Value);
    }

    [Fact]
    public async Task EncryptionKeyFile_WorksWithBase64File()
    {
        var keyFilePath = Path.Combine(_tempDir, "keyfile");
        await File.WriteAllTextAsync(keyFilePath, _encryptionKey);

        await using var provider = new FileSystemSecretProvider(new FileSystemOptions
        {
            BasePath = Path.Combine(_tempDir, "keyfile-store"),
            EncryptionKeyFile = keyFilePath
        });

        await provider.PutSecretAsync("kf-secret", "keyfile-value");
        var retrieved = await provider.GetSecretAsync("kf-secret");

        Assert.Equal("keyfile-value", retrieved.Value);
    }

    [Fact]
    public async Task GetVersions_ThrowsWhenSecretNotFound()
    {
        await using var provider = CreateProvider();

        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync("nonexistent"));
    }

    // --- security: path traversal via the key (audit FS-2) ---

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    public async Task GetSecret_RejectsTraversingKey(string key)
    {
        await using var provider = CreateProvider();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.GetSecretAsync(key));
    }

    [Fact]
    public async Task PutSecret_WithDotKey_DoesNotWriteOutsideStore()
    {
        await using var provider = CreateProvider();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.PutSecretAsync(".", "value"));
    }

    [Fact]
    public async Task DeleteSecret_WithDotKey_DoesNotDeleteStore()
    {
        await using var provider = CreateProvider();
        await provider.PutSecretAsync("real-secret", "keep-me");

        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.DeleteSecretAsync("."));

        // A "." key used to resolve to BasePath itself, so the recursive delete wiped the store.
        Assert.True(await provider.SecretExistsAsync("real-secret"));
    }

    // --- security: path traversal via the version query parameter (audit FS-1) ---

    [Theory]
    [InlineData("../other/v1")]
    [InlineData("/etc/hosts")]
    [InlineData("current")]
    [InlineData("v1/../v2")]
    [InlineData("1")]
    public async Task GetSecret_RejectsInvalidVersion(string version)
    {
        await using var provider = CreateProvider();
        await provider.PutSecretAsync("versioned", "value");

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.GetSecretAsync("versioned", new SecretQuery { Version = version }));
    }

    // --- security: encryption downgrade / missing-key mismatch (audit FS-3) ---

    [Fact]
    public async Task GetSecret_EncryptionConfiguredButEnvelopePlaintext_Throws()
    {
        // A store written without a key, then read with one configured: the planted-plaintext attack.
        // Anyone able to write to the store could set "encrypted": false and a chosen value.
        await using (var plaintextProvider = CreateProvider())
            await plaintextProvider.PutSecretAsync("planted", "attacker-value");

        await using var keyedProvider = CreateProvider(encrypt: true);

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => keyedProvider.GetSecretAsync("planted"));

        Assert.Contains("unencrypted", ex.Message);
    }

    [Fact]
    public async Task GetSecret_EncryptedEnvelopeButNoKeyConfigured_Throws()
    {
        await using (var keyedProvider = CreateProvider(encrypt: true))
            await keyedProvider.PutSecretAsync("cipher", "cipher-value");

        await using var plaintextProvider = CreateProvider();

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => plaintextProvider.GetSecretAsync("cipher"));

        Assert.Contains("no encryption key is configured", ex.Message);
    }
}
