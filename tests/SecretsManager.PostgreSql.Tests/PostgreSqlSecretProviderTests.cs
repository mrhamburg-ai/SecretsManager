using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.Internal;
using SecretsManager.PostgreSql.Internal;

namespace SecretsManager.PostgreSql.Tests;

public sealed class PostgreSqlSecretProviderTests
{
    private readonly IPostgreSqlSecretStore _mockStore = Substitute.For<IPostgreSqlSecretStore>();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    /// <summary>A provider with no encryption key configured.</summary>
    private PostgreSqlSecretProvider CreateProvider() =>
        new(_mockStore, new EncryptionKeyResolver(null, null, null, null));

    /// <summary>A provider with a real 32-byte AES-256 key, so the crypto path is exercised for real.</summary>
    private PostgreSqlSecretProvider CreateProviderWithKey() =>
        new(_mockStore, new EncryptionKeyResolver(Convert.ToBase64String(_key), null, null, null));

    /// <summary>Builds the stored JSON for an encrypted row, using the same format the provider writes.</summary>
    private string EncryptedPayloadFor(string plaintext)
    {
        using var encryptor = new AesGcmEncryptor(_key);
        var payload = encryptor.Encrypt(Encoding.UTF8.GetBytes(plaintext));

        return JsonSerializer.Serialize(new
        {
            n = Convert.ToBase64String(payload.Nonce),
            c = Convert.ToBase64String(payload.Ciphertext),
            t = Convert.ToBase64String(payload.Tag),
            k = (string?)null
        });
    }

    private static PostgreSqlSecretRow MakeSecret(int id = 1, string key = "my-secret") =>
        new(id, key, "text/plain",
            new Dictionary<string, string> { ["env"] = "prod" },
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"));

    private static PostgreSqlVersionRow MakeVersion(
        int secretId = 1, int version = 1, string value = "secret-value",
        bool encrypted = false, bool isCurrent = true) =>
        new(version, secretId, version, value, encrypted,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"), isCurrent);

    [Fact]
    public async Task GetSecretAsync_ReturnsSecretValue()
    {
        _mockStore.GetSecretByKeyAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret());
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion());

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret");

        Assert.Equal("my-secret", result.Key);
        Assert.Equal("secret-value", result.Value);
        Assert.Equal("1", result.Version);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), result.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), result.UpdatedAt);
        Assert.Equal("text/plain", result.ContentType);
        Assert.NotNull(result.Tags);
        Assert.Equal("prod", result.Tags!["env"]);
    }

    [Fact]
    public async Task GetSecretAsync_WithVersion_PassesVersionToStore()
    {
        _mockStore.GetSecretByKeyAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret());
        _mockStore.GetVersionAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(version: 2, value: "old-value"));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("my-secret", new SecretQuery { Version = "2" });

        Assert.Equal("2", result.Version);
        Assert.Equal("old-value", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithProperty_ExtractsJsonProperty()
    {
        _mockStore.GetSecretByKeyAsync("db-config", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "db-config"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(value: """{"host":"db.example.com","port":5432}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("db-config", new SecretQuery { Property = "host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_WithNestedProperty_ExtractsNestedValue()
    {
        _mockStore.GetSecretByKeyAsync("config", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "config"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(value: """{"db":{"host":"db.example.com"}}"""));

        await using var provider = CreateProvider();
        var result = await provider.GetSecretAsync("config", new SecretQuery { Property = "db.host" });

        Assert.Equal("db.example.com", result.Value);
    }

    [Fact]
    public async Task GetSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockStore.GetSecretByKeyAsync("missing", Arg.Any<CancellationToken>())
            .Returns((PostgreSqlSecretRow?)null);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task GetSecretAsync_VersionNotFound_ThrowsSecretNotFoundException()
    {
        _mockStore.GetSecretByKeyAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret());
        _mockStore.GetVersionAsync(1, 99, Arg.Any<CancellationToken>())
            .Returns((PostgreSqlVersionRow?)null);

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretAsync("my-secret", new SecretQuery { Version = "99" }));
    }

    [Fact]
    public async Task GetSecretAsync_InvalidVersion_ThrowsSecretProviderException()
    {
        _mockStore.GetSecretByKeyAsync("my-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret());

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("my-secret", new SecretQuery { Version = "abc" }));

        Assert.Contains("Invalid version", ex.Message);
    }

    [Fact]
    public async Task GetSecretAsync_StoreError_ThrowsSecretProviderException()
    {
        _mockStore.GetSecretByKeyAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("broken"));
    }

    [Fact]
    public async Task GetSecretAsync_EncryptedValue_DecryptsSuccessfully()
    {
        var plaintext = "my-secret-value";
        var encryptedJson = EncryptedPayloadFor(plaintext);

        _mockStore.GetSecretByKeyAsync("encrypted-secret", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "encrypted-secret"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(value: encryptedJson, encrypted: true));

        await using var provider = CreateProviderWithKey();
        var result = await provider.GetSecretAsync("encrypted-secret");

        Assert.Equal(plaintext, result.Value);
    }

    [Fact]
    public async Task PutSecretAsync_CreatesSecretAndVersion()
    {
        _mockStore.PutSecretAsync("new-secret", "new-value", false, null, null, Arg.Any<CancellationToken>())
            .Returns((MakeSecret(key: "new-secret"), MakeVersion(value: "new-value")));

        await using var provider = CreateProvider();
        var result = await provider.PutSecretAsync("new-secret", "new-value");

        Assert.Equal("new-secret", result.Key);
        Assert.Equal("new-value", result.Value);
        Assert.Equal("1", result.Version);
    }

    [Fact]
    public async Task PutSecretAsync_WithMetadata_PassesContentTypeAndTags()
    {
        var tags = new Dictionary<string, string> { ["env"] = "staging" }.AsReadOnly();
        _mockStore.PutSecretAsync("tagged", "val", false, "application/json", tags, Arg.Any<CancellationToken>())
            .Returns((
                MakeSecret(key: "tagged"),
                MakeVersion(value: "val")));

        await using var provider = CreateProvider();
        await provider.PutSecretAsync("tagged", "val",
            new SecretMetadata { ContentType = "application/json", Tags = tags });

        await _mockStore.Received(1).PutSecretAsync(
            "tagged", "val", false, "application/json", tags, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSecretAsync_WithEncryption_EncryptsValueBeforeStorage()
    {
        string? storedValue = null;
        _mockStore.PutSecretAsync(
                Arg.Is("enc-secret"), Arg.Do<string>(v => storedValue = v), Arg.Is(true),
                Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns((MakeSecret(key: "enc-secret"), MakeVersion(value: "encrypted", encrypted: true)));

        await using var provider = CreateProviderWithKey();
        await provider.PutSecretAsync("enc-secret", "plaintext-value");

        Assert.NotNull(storedValue);
        Assert.DoesNotContain("plaintext-value", storedValue!);

        // The stored payload must decrypt back to the original value with the same key.
        using var doc = JsonDocument.Parse(storedValue!);
        using var encryptor = new AesGcmEncryptor(_key);
        var decrypted = encryptor.Decrypt(new EncryptedPayload(
            Convert.FromBase64String(doc.RootElement.GetProperty("n").GetString()!),
            Convert.FromBase64String(doc.RootElement.GetProperty("c").GetString()!),
            Convert.FromBase64String(doc.RootElement.GetProperty("t").GetString()!)));

        Assert.Equal("plaintext-value", Encoding.UTF8.GetString(decrypted));
    }

    [Fact]
    public async Task PutSecretAsync_StoreError_ThrowsSecretProviderException()
    {
        _mockStore.PutSecretAsync("broken", "val", false, null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.PutSecretAsync("broken", "val"));
    }

    [Fact]
    public async Task DeleteSecretAsync_DeletesSecret()
    {
        _mockStore.DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();
        await provider.DeleteSecretAsync("to-delete");

        await _mockStore.Received(1).DeleteSecretAsync("to-delete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_NotFound_ThrowsSecretNotFoundException()
    {
        _mockStore.DeleteSecretAsync("missing", Arg.Any<CancellationToken>())
            .Returns(false);

        await using var provider = CreateProvider();
        var ex = await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.DeleteSecretAsync("missing"));

        Assert.Equal("missing", ex.Key);
    }

    [Fact]
    public async Task DeleteSecretAsync_StoreError_ThrowsSecretProviderException()
    {
        _mockStore.DeleteSecretAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.DeleteSecretAsync("broken"));
    }

    [Fact]
    public async Task GetSecretVersionsAsync_ReturnsAllVersionsWithCurrentMarked()
    {
        _mockStore.GetSecretByKeyAsync("versioned", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "versioned"));
        _mockStore.ListVersionsAsync(1, Arg.Any<CancellationToken>())
            .Returns([
                MakeVersion(version: 1, isCurrent: false),
                MakeVersion(version: 2, isCurrent: false),
                MakeVersion(version: 3, isCurrent: true)
            ]);

        await using var provider = CreateProvider();
        var versions = await provider.GetSecretVersionsAsync("versioned");

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
        _mockStore.GetSecretByKeyAsync("missing", Arg.Any<CancellationToken>())
            .Returns((PostgreSqlSecretRow?)null);

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => provider.GetSecretVersionsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrue_WhenSecretExists()
    {
        _mockStore.SecretExistsAsync("exists", Arg.Any<CancellationToken>())
            .Returns(true);

        await using var provider = CreateProvider();
        Assert.True(await provider.SecretExistsAsync("exists"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalse_WhenNotFound()
    {
        _mockStore.SecretExistsAsync("missing", Arg.Any<CancellationToken>())
            .Returns(false);

        await using var provider = CreateProvider();
        Assert.False(await provider.SecretExistsAsync("missing"));
    }

    [Fact]
    public async Task SecretExistsAsync_StoreError_ThrowsSecretProviderException()
    {
        _mockStore.SecretExistsAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        await using var provider = CreateProvider();
        await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.SecretExistsAsync("broken"));
    }

    [Fact]
    public async Task GetSecretAsync_EncryptionConfiguredButRowPlaintext_Throws()
    {
        // Regression test for the downgrade attack. This used to assert that the plaintext row was
        // returned, which is exactly what UPDATE ... SET encrypted = false, value = 'attacker-value'
        // achieves for anyone with database access.
        _mockStore.GetSecretByKeyAsync("plain", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "plain"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(value: "plaintext", encrypted: false));

        await using var provider = CreateProviderWithKey();

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("plain"));

        Assert.Contains("unencrypted", ex.Message);
    }

    [Fact]
    public async Task GetSecretAsync_EncryptedRowButNoKeyConfigured_Throws()
    {
        _mockStore.GetSecretByKeyAsync("cipher", Arg.Any<CancellationToken>())
            .Returns(MakeSecret(key: "cipher"));
        _mockStore.GetVersionAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(MakeVersion(value: """{"n":"","c":"","t":""}""", encrypted: true));

        await using var provider = CreateProvider();

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => provider.GetSecretAsync("cipher"));

        Assert.Contains("no encryption key is configured", ex.Message);
    }
}
