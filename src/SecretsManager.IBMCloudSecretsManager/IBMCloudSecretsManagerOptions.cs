namespace SecretsManager.IBMCloudSecretsManager;

public sealed class IBMCloudSecretsManagerOptions
{
    /// <summary>
    /// Alibaba Cloud region (e.g. "eu-central-1")
    /// </summary>
    public string Region { get; set; } = "";
    /// <summary>
    /// IBM Cloud instance ID for the Secrets Manager instance
    /// </summary>
    public string InstanceId { get; set; } = "";
    /// <summary>
    /// IBM Cloud API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = "";
    /// <summary>
    /// Service URL override (optional)
    /// </summary>
    public string? ServiceUrl { get; set; }

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
