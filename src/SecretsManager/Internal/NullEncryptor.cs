namespace SecretsManager.Internal;

/// <summary>
/// No-op <see cref="ISecretEncryptor"/> used when a provider has no encryption configured.
/// Values pass through unencrypted.
/// </summary>
public sealed class NullEncryptor : ISecretEncryptor
{
    /// <summary>
    /// Always <see langword="false"/>: this encryptor does not encrypt.
    /// </summary>
    public bool IsEnabled => false;

    /// <summary>
    /// Returns the plaintext unchanged as the "ciphertext", with an empty nonce and tag.
    /// </summary>
    /// <param name="plaintext">The data to pass through.</param>
    /// <param name="associatedData">Ignored: nothing is authenticated when encryption is off.</param>
    /// <returns>A payload whose ciphertext is the original plaintext.</returns>
    public EncryptedPayload Encrypt(byte[] plaintext, byte[]? associatedData = null) =>
        new(Nonce: [], Ciphertext: plaintext, Tag: []);

    /// <summary>
    /// Returns the payload's bytes unchanged.
    /// </summary>
    /// <param name="payload">The payload to read.</param>
    /// <returns>The original plaintext bytes.</returns>
    public byte[] Decrypt(EncryptedPayload payload) =>
        payload.Ciphertext;

    /// <summary>
    /// No resources to release.
    /// </summary>
    public void Dispose() { }
}
