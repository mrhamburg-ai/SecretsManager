namespace SecretsManager.AliyunKms;

/// <summary>
/// Options for the Alibaba Cloud KMS secret provider.
/// </summary>
public sealed class AliyunKmsOptions
{
    /// <summary>
    /// Alibaba Cloud region (e.g. "eu-central-1").
    /// </summary>
    public string Region { get; set; } = "";

    /// <summary>
    /// Alibaba Cloud access key ID.
    /// </summary>
    public string AccessKeyId { get; set; } = "";

    /// <summary>
    /// Alibaba Cloud access key secret.
    /// </summary>
    public string AccessKeySecret { get; set; } = "";

    /// <summary>
    /// Custom API endpoint (optional).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Validates the options and returns error messages for any missing required fields.
    /// </summary>
    /// <returns>An enumerable of validation error messages; empty if the options are valid.</returns>
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
