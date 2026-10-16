namespace SecretsManager.Vault;

public sealed class VaultOptions
{
    public string Url { get; set; } = "http://localhost:8200";
    public string Token { get; set; } = "";
    public string MountPath { get; set; } = "secret";
    public bool SkipTlsVerify { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            yield return "Url is required";
        if (!Url.StartsWith("http://") && !Url.StartsWith("https://"))
            yield return $"Url '{Url}' must start with http:// or https://";
    }
}
