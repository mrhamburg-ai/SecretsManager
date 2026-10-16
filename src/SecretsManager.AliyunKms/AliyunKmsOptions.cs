namespace SecretsManager.AliyunKms;

public sealed class AliyunKmsOptions
{
    public string Region { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string AccessKeySecret { get; set; } = "";
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
