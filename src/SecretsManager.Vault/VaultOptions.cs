namespace SecretsManager.Vault;

/// <summary>
/// Options for the HashiCorp Vault secret provider.
/// </summary>
public sealed class VaultOptions
{
    /// <summary>
    /// Vault server URL (e.g. "http://localhost:8200").
    /// </summary>
    public string Url { get; set; } = "http://localhost:8200";

    /// <summary>
    /// Authentication token for Vault.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// KV mount path where secrets are stored.
    /// </summary>
    public string MountPath { get; set; } = "secret";

    /// <summary>
    /// When true, disables TLS certificate validation.
    /// </summary>
    public bool SkipTlsVerify { get; set; }

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// </summary>
    /// <returns>An enumerable of validation error messages. Empty when the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        var url = Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            yield return "Url is required";
            yield break;
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            yield return $"Url '{url}' must start with http:// or https://";
    }
}
