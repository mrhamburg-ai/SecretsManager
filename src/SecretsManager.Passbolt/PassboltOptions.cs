namespace SecretsManager.Passbolt;

public sealed class PassboltOptions
{
    /// <summary>
    /// Base URL of the service endpoint
    /// </summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>
    /// User's private key for authentication
    /// </summary>
    public string UserPrivateKey { get; set; } = "";
    /// <summary>
    /// User's private key for authentication
    /// </summary>
    public string UserPrivateKeyPassphrase { get; set; } = "";
    /// <summary>
    /// Fingerprint of the user's public key
    /// </summary>
    public string UserKeyFingerprint { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            yield return "BaseUrl is required";
        if (string.IsNullOrWhiteSpace(UserPrivateKey))
            yield return "UserPrivateKey is required";
        if (string.IsNullOrWhiteSpace(UserKeyFingerprint))
            yield return "UserKeyFingerprint is required";
    }
}
