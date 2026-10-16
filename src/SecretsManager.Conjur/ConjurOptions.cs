namespace SecretsManager.Conjur;

public sealed class ConjurOptions
{
    /// <summary>
    /// Base URL of the service endpoint
    /// </summary>
    public string Url { get; set; } = "https://localhost:443";
    /// <summary>
    /// Organization account name the server was configured with
    /// </summary>
    public string Account { get; set; } = "";
    /// <summary>
    /// Login name to authenticate with
    /// </summary>
    public string Login { get; set; } = "";
    /// <summary>
    /// IBM Cloud API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = "";
    /// <summary>
    /// Policy branch targeted when deleting secrets
    /// </summary>
    public string PolicyPath { get; set; } = "root";
    /// <summary>
    /// When true, disables TLS certificate validation
    /// </summary>
    public bool SkipTlsVerify { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            yield return "Url is required";
        if (string.IsNullOrWhiteSpace(Account))
            yield return "Account is required";
        if (string.IsNullOrWhiteSpace(Login))
            yield return "Login is required";
        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return "ApiKey is required";
    }
}
