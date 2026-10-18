using System.Text;
using SecretsManager.Internal;

namespace SecretsManager;

/// <summary>
/// How the key material read from a secret is encoded.
/// </summary>
public enum SecretKeyEncoding
{
    /// <summary>Standard base64. This is the default, and matches <c>openssl rand -base64 32</c>.</summary>
    Base64,

    /// <summary>
    /// Raw UTF-8 text, used as-is. Requires a value of exactly 32 characters.
    /// </summary>
    /// <remarks>
    /// This is a passphrase, not a key: 32 characters of text carry far less entropy than 32 random
    /// bytes, so it is far more vulnerable to brute force. Prefer <see cref="Base64"/> with
    /// <c>openssl rand -base64 32</c>, and use this only when the key must already exist as text.
    /// </remarks>
    Utf8
}

/// <summary>
/// Reads the encryption key from a secret stored in another <see cref="ISecretProvider"/>, so a
/// filesystem or PostgreSQL store can be encrypted with a key held in Scaleway, Azure Key Vault,
/// Vault, or any other provider.
/// </summary>
/// <remarks>
/// The key id is derived from the version the source reports
/// (<c>{key}@{version}</c>), so rotating the key is a single <c>PutSecretAsync</c> at the source:
/// new writes pick up the new version and existing ciphertext still decrypts because
/// <see cref="GetKeyAsync"/> fetches the version it was written with.
/// This class does not dispose the provider it wraps — the caller owns it.
/// </remarks>
public sealed class SecretProviderKeySource : IEncryptionKeyProvider
{
    private readonly ISecretProvider _provider;
    private readonly string _key;
    private readonly SecretKeyEncoding _encoding;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretProviderKeySource"/> class.
    /// </summary>
    /// <param name="provider">The provider holding the key. Not disposed by this class.</param>
    /// <param name="key">The secret key to read, for example <c>sm/data-key</c>.</param>
    /// <param name="encoding">How the stored value is decoded. Defaults to base64.</param>
    public SecretProviderKeySource(
        ISecretProvider provider,
        string key,
        SecretKeyEncoding encoding = SecretKeyEncoding.Base64)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _provider = provider;
        _key = key;
        _encoding = encoding;
    }

    /// <summary>
    /// Reads the current version of the key secret.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current key and its <c>{key}@{version}</c> identifier.</returns>
    public async ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default)
    {
        var value = await _provider.GetSecretAsync(_key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new EncryptionKeyMaterial(Decode(value.Value), BuildKeyId(value.Key, value.Version));
    }

    /// <summary>
    /// Reads a specific version of the key secret, for decrypting values written before the key was
    /// rotated.
    /// </summary>
    /// <param name="keyId">An identifier previously returned by <see cref="GetCurrentKeyAsync"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key for that version.</returns>
    /// <exception cref="SecretProviderException">The identifier belongs to a different key.</exception>
    public async ValueTask<EncryptionKeyMaterial> GetKeyAsync(
        string keyId, CancellationToken cancellationToken = default)
    {
        var (key, version) = ParseKeyId(keyId);

        if (!string.Equals(key, _key, StringComparison.Ordinal))
            throw new SecretProviderException(
                $"Key id '{keyId}' does not belong to this key source, which reads '{_key}'.");

        var value = await _provider
            .GetSecretAsync(_key, new SecretQuery { Version = version }, cancellationToken)
            .ConfigureAwait(false);

        return new EncryptionKeyMaterial(Decode(value.Value), keyId);
    }

    private byte[] Decode(string value)
    {
        byte[] bytes;
        try
        {
            bytes = _encoding == SecretKeyEncoding.Base64
                ? Convert.FromBase64String(value.Trim())
                : Encoding.UTF8.GetBytes(value);
        }
        catch (FormatException ex)
        {
            // Report this as a provider error, not a raw FormatException, so callers can handle
            // every key-resolution failure the same way.
            throw new SecretProviderException(
                $"Key secret '{_key}' is not valid {(_encoding == SecretKeyEncoding.Base64 ? "base64" : "UTF-8")}.", ex);
        }

        if (bytes.Length != 32)
            throw new SecretProviderException(
                $"Key secret '{_key}' resolved to {bytes.Length} bytes; AES-256-GCM requires 32.");

        return bytes;
    }

    private static string BuildKeyId(string key, string? version)
    {
        // Fail closed. Falling back to the bare key name would make the key id stable across a
        // rotation: new writes would record the same id as values written under the previous key,
        // and after a restart the old values would be decrypted with the new key and fail with a
        // tag mismatch rather than a clear error. A source that cannot report versions must not be
        // used as a key source.
        if (string.IsNullOrEmpty(version))
            throw new SecretProviderException(
                $"The key source for '{key}' reported no version. A key id is derived from the " +
                "source's version, so a source without versions cannot safely be used as a key " +
                "source: rotating the key would reuse the same key id and make existing values " +
                "unreadable.");

        return $"{key}@{version}";
    }

    private static (string Key, string? Version) ParseKeyId(string keyId)
    {
        var at = keyId.LastIndexOf('@');
        return at < 0
            ? (keyId, null)
            : (keyId[..at], keyId[(at + 1)..]);
    }
}
