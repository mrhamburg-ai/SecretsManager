namespace SecretsManager.TencentCloud;

/// <summary>
/// Options for the Tencent Cloud secret provider.
/// </summary>
public sealed class TencentCloudOptions
{
    /// <summary>
    /// Tencent Cloud region (e.g. "ap-shanghai").
    /// </summary>
    public string Region { get; set; } = "";

    /// <summary>
    /// Tencent Cloud secret ID.
    /// </summary>
    public string SecretId { get; set; } = "";

    /// <summary>
    /// Tencent Cloud secret key.
    /// </summary>
    public string SecretKey { get; set; } = "";

    /// <summary>
    /// Custom API endpoint (optional).
    /// </summary>
    public string? Endpoint { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (string.IsNullOrWhiteSpace(SecretId))
            yield return "SecretId is required";
        if (string.IsNullOrWhiteSpace(SecretKey))
            yield return "SecretKey is required";
    }
}
