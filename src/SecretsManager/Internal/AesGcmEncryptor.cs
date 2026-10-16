using System.Security.Cryptography;

namespace SecretsManager.Internal;

/// <summary>
/// <see cref="ISecretEncryptor"/> implementation that encrypts payloads with AES-256-GCM,
/// using a random 96-bit nonce and a 128-bit authentication tag per payload.
/// </summary>
public sealed class AesGcmEncryptor : ISecretEncryptor
{
    private const int NonceSize = 12; // 96 bits, recommended for AES-GCM
    private const int TagSize = 16;   // 128 bits

    private readonly AesGcm _aesGcm;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmEncryptor"/> class.
    /// </summary>
    /// <param name="key">The 256-bit (32-byte) AES key.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is not 32 bytes long.</exception>
    public AesGcmEncryptor(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("AES-256-GCM requires a 256-bit (32-byte) key.", nameof(key));

        _aesGcm = new AesGcm(key, TagSize);
    }

    /// <summary>
    /// Always <see langword="true"/>: this encryptor always encrypts.
    /// </summary>
    public bool IsEnabled => true;

    /// <summary>
    /// Encrypts the plaintext with AES-256-GCM using a freshly generated nonce.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <returns>The nonce, ciphertext and authentication tag for this payload.</returns>
    public EncryptedPayload Encrypt(byte[] plaintext)
    {
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        _aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return new EncryptedPayload(nonce, ciphertext, tag);
    }

    /// <summary>
    /// Decrypts and authenticates a payload produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="payload">The payload to decrypt.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    /// <exception cref="AuthenticationTagMismatchException">
    /// Thrown when the tag does not match, i.e. the data was tampered with or the key is wrong.
    /// </exception>
    public byte[] Decrypt(EncryptedPayload payload)
    {
        var plaintext = new byte[payload.Ciphertext.Length];
        _aesGcm.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext);
        return plaintext;
    }

    /// <summary>
    /// Releases the underlying <see cref="AesGcm"/> instance.
    /// </summary>
    public void Dispose() => _aesGcm.Dispose();
}
