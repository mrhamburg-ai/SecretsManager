namespace SecretsManager.IBMCloudSecretsManager;

/// <summary>
/// Configuration options for the IBM Cloud Secrets Manager provider.
/// </summary>
public sealed class IBMCloudSecretsManagerOptions
{
    /// <summary>
    /// IBM Cloud region (e.g. "us-south", "eu-de", "jp-tok").
    /// </summary>
    public string Region { get; set; } = "";

    /// <summary>
    /// IBM Cloud instance ID for the Secrets Manager instance.
    /// </summary>
    public string InstanceId { get; set; } = "";

    /// <summary>
    /// IBM Cloud API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// IBM Cloud service URL override (optional).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// </summary>
    /// <returns>An enumerable of validation error messages. An empty collection indicates the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (string.IsNullOrWhiteSpace(InstanceId))
            yield return "InstanceId is required";
        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return "ApiKey is required";
    }
}
