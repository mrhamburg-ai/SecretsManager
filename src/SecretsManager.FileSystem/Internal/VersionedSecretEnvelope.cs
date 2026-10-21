using System.Text.Json.Serialization;

namespace SecretsManager.FileSystem.Internal;

/// <summary>
/// On-disk JSON representation of a single secret version.
/// When encrypted, <see cref="Nonce"/>, <see cref="Ciphertext"/>, and <see cref="AuthTag"/>
/// hold base64-encoded binary data. When unencrypted, <see cref="Value"/> holds the plaintext.
/// </summary>
internal sealed class VersionedSecretEnvelope
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("encrypted")]
    public bool Encrypted { get; set; }

    /// <summary>
    /// Identifier of the key that produced this ciphertext, so the value stays readable after the
    /// key is rotated at its source. Null when the value was written with the static key or key file.
    /// </summary>
    [JsonPropertyName("keyId")]
    public string? KeyId { get; set; }

    // Encrypted fields (base64)
    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("ciphertext")]
    public string? Ciphertext { get; set; }

    [JsonPropertyName("authTag")]
    public string? AuthTag { get; set; }

    // Plaintext field (when encryption is disabled)
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
