namespace SecretsManager.GoogleSecretManager;

public sealed class GoogleSecretManagerOptions
{
    /// <summary>
    /// Scaleway project UUID
    /// </summary>
    public string ProjectId { get; set; } = "";
    /// <summary>
    /// Path to a service account credentials JSON file
    /// </summary>
    public string? CredentialsPath { get; set; }
    /// <summary>
    /// Inline service account credentials JSON content
    /// </summary>
    public string? CredentialsJson { get; set; }
    /// <summary>
    /// Custom API endpoint (optional)
    /// </summary>
    public string? Endpoint { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            yield return "ProjectId is required";
    }
}
