namespace SecretsManager.GoogleSecretManager;

public sealed class GoogleSecretManagerOptions
{
    public string ProjectId { get; set; } = "";
    public string? CredentialsPath { get; set; }
    public string? CredentialsJson { get; set; }
    public string? Endpoint { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            yield return "ProjectId is required";
    }
}
