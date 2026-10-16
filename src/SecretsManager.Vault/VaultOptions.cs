namespace SecretsManager.Vault;

public sealed class VaultOptions
{
    /// <summary>
    /// Base URL of the service endpoint
    /// </summary>
    public string Url { get; set; } = "http://localhost:8200";
    /// <summary>
    /// Authentication token or API key
    /// </summary>
    public string Token { get; set; } = "";
    /// <summary>
    /// KV mount path where secrets are stored
    /// </summary>
    public string MountPath { get; set; } = "secret";
    /// <summary>
    /// When true, disables TLS certificate validation
    /// </summary>
    public bool SkipTlsVerify { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            yield return "Url is required";
        if (!Url.StartsWith("http://") && !Url.StartsWith("https://"))
            yield return $"Url '{Url}' must start with http:// or https://";
    }
}
