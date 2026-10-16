namespace SecretsManager.GoogleSecretManager;

/// <summary>
/// Configuration options for the Google Cloud Secret Manager secret provider.
/// </summary>
public sealed class GoogleSecretManagerOptions
{
    /// <summary>
    /// Google Cloud project ID (required).
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Path to a service account credentials JSON file.
    /// If not set, uses Application Default Credentials.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Inline service account credentials JSON content.
    /// If not set, uses Application Default Credentials or CredentialsPath.
    /// </summary>
    public string? CredentialsJson { get; set; }

    /// <summary>
    /// Custom API endpoint (e.g. for emulators).
    /// If not set, uses the default secretmanager.googleapis.com.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Validates the options and returns error messages.
    /// </summary>
    /// <returns>Validation error messages. An empty collection indicates the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            yield return "ProjectId is required";
    }
}
