namespace SecretsManager.Internal;

/// <summary>
/// An encrypted secret payload produced by <see cref="ISecretEncryptor"/>.
/// </summary>
/// <param name="Nonce">The nonce (initialization vector) generated for this payload.</param>
/// <param name="Ciphertext">The encrypted secret bytes.</param>
/// <param name="Tag">The authentication tag used to verify the ciphertext.</param>
/// <param name="AssociatedData">
/// The authenticated associated data, if any. Not secret, but covered by the authentication tag, so
/// tampering with it is detected on decrypt.
/// </param>
public record EncryptedPayload(byte[] Nonce, byte[] Ciphertext, byte[] Tag, byte[]? AssociatedData = null);

/// <summary>
/// Encrypts and decrypts secret payloads for providers that support encryption at rest.
/// </summary>
public interface ISecretEncryptor : IDisposable
{
    /// <summary>
    /// Whether encryption is actually applied. <see langword="false"/> for the pass-through
    /// <see cref="NullEncryptor"/>.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Encrypts the given plaintext bytes.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="associatedData">
    /// Optional data to authenticate but not encrypt, such as the key id and the secret's own
    /// identity. It is covered by the tag, so moving a payload to a different secret, version, or key
    /// is detected on decrypt instead of silently returning the wrong value.
    /// </param>
    /// <returns>The encrypted payload, including its nonce and authentication tag.</returns>
    EncryptedPayload Encrypt(byte[] plaintext, byte[]? associatedData = null);

    /// <summary>
    /// Decrypts a payload previously produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="payload">The encrypted payload to decrypt.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    byte[] Decrypt(EncryptedPayload payload);
}
