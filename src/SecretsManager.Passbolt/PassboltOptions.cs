namespace SecretsManager.Passbolt;

public sealed class PassboltOptions
{
    public string BaseUrl { get; set; } = "";
    public string UserPrivateKey { get; set; } = "";
    public string UserPrivateKeyPassphrase { get; set; } = "";
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
