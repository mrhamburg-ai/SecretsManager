namespace SecretsManager;

/// <summary>
/// A resolved encryption key and the identifier of the version that produced it.
/// </summary>
/// <param name="Key">The 32-byte AES-256 key.</param>
/// <param name="KeyId">Stable identifier for this key version, recorded alongside the ciphertext.</param>
public readonly record struct EncryptionKeyMaterial(byte[] Key, string KeyId);

/// <summary>
/// Supplies the key-encryption key used to protect secrets at rest, so the key does not have to
/// live in configuration, an environment variable, or a file.
/// Implementations must be thread-safe.
/// </summary>
public interface IEncryptionKeyProvider
{
    /// <summary>
    /// Resolves the key to use for new writes. Must return 32 bytes for AES-256-GCM.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current key and the identifier of the version that produced it.</returns>
    ValueTask<EncryptionKeyMaterial> GetCurrentKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a specific historical key, identified by the <c>keyId</c> recorded alongside the
    /// ciphertext, so existing values stay readable after the key is rotated at the source.
    /// </summary>
    /// <param name="keyId">An identifier previously returned by <see cref="GetCurrentKeyAsync"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key that produced the given identifier.</returns>
    ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default);
}
