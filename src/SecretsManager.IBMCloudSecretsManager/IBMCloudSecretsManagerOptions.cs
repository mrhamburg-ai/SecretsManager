namespace SecretsManager.IBMCloudSecretsManager;

public sealed class IBMCloudSecretsManagerOptions
{
    public string Region { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public string ApiKey { get; set; } = "";
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
