namespace SecretsManager.AliyunKms;

public sealed class AliyunKmsOptions
{
    /// <summary>
    /// Alibaba Cloud region (e.g. "eu-central-1")
    /// </summary>
    public string Region { get; set; } = "";
    /// <summary>
    /// Scaleway access key
    /// </summary>
    public string AccessKeyId { get; set; } = "";
    /// <summary>
    /// Scaleway access key
    /// </summary>
    public string AccessKeySecret { get; set; } = "";
    /// <summary>
    /// Custom API endpoint (optional)
    /// </summary>
    public string? Endpoint { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (string.IsNullOrWhiteSpace(AccessKeyId))
            yield return "AccessKeyId is required";
        if (string.IsNullOrWhiteSpace(AccessKeySecret))
            yield return "AccessKeySecret is required";
    }
}
