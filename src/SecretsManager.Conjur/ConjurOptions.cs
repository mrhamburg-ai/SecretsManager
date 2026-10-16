namespace SecretsManager.Conjur;

/// <summary>
/// Options for the CyberArk Conjur secret provider.
/// </summary>
public sealed class ConjurOptions
{
    /// <summary>
    /// Base URL of the Conjur server (e.g. https://conjur.example.com:443).
    /// </summary>
    public string Url { get; set; } = "https://localhost:443";

    /// <summary>
    /// Organization account name the server was configured with.
    /// </summary>
    public string Account { get; set; } = "";

    /// <summary>
    /// Login name to authenticate with. For a host, prefix the host id with <c>host/</c>.
    /// </summary>
    public string Login { get; set; } = "";

    /// <summary>
    /// API key (or password for users) used to obtain a short-lived access token.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Policy branch targeted when deleting secrets. Conjur has no direct delete API,
    /// so deletion is performed by patching a policy with a <c>!delete</c> statement.
    /// Defaults to the root policy.
    /// </summary>
    public string PolicyPath { get; set; } = "root";

    /// <summary>
    /// When true, disables TLS certificate validation (useful for testing with self-signed certs).
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
