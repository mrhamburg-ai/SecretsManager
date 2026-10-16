namespace SecretsManager.Scaleway;

public sealed class ScalewayOptions
{
    /// <summary>
    /// Alibaba Cloud region (e.g. "eu-central-1")
    /// </summary>
    public string Region { get; set; } = "";
    /// <summary>
    /// Scaleway project UUID
    /// </summary>
    public string ProjectId { get; set; } = "";
    /// <summary>
    /// Scaleway access key
    /// </summary>
    public string AccessKey { get; set; } = "";
    /// <summary>
    /// Tencent Cloud secret key
    /// </summary>
    public string SecretKey { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (string.IsNullOrWhiteSpace(ProjectId))
            yield return "ProjectId is required";
        if (string.IsNullOrWhiteSpace(AccessKey))
            yield return "AccessKey is required";
        if (string.IsNullOrWhiteSpace(SecretKey))
            yield return "SecretKey is required";
    }
}
