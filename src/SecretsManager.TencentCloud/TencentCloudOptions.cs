namespace SecretsManager.TencentCloud;

public sealed class TencentCloudOptions
{
    public string Region { get; set; } = "";

    public string SecretId { get; set; } = "";

    public string SecretKey { get; set; } = "";

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
