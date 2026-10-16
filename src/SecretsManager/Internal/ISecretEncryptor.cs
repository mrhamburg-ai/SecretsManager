namespace SecretsManager.Internal;

/// <summary>
/// An encrypted secret payload produced by <see cref="ISecretEncryptor"/>.
/// </summary>
/// <param name="Nonce">The nonce (initialization vector) generated for this payload.</param>
/// <param name="Ciphertext">The encrypted secret bytes.</param>
/// <param name="Tag">The authentication tag used to verify the ciphertext.</param>
public record EncryptedPayload(byte[] Nonce, byte[] Ciphertext, byte[] Tag);

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
    /// <returns>The encrypted payload, including its nonce and authentication tag.</returns>
    EncryptedPayload Encrypt(byte[] plaintext);

    /// <summary>
    /// Decrypts a payload previously produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="payload">The encrypted payload to decrypt.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    byte[] Decrypt(EncryptedPayload payload);
}
