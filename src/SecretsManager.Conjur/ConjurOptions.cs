namespace SecretsManager.Conjur;

public sealed class ConjurOptions
{
    public string Url { get; set; } = "https://localhost:443";
    public string Account { get; set; } = "";
    public string Login { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string PolicyPath { get; set; } = "root";
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
