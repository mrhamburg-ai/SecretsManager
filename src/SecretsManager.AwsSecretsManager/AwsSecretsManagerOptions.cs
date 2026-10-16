namespace SecretsManager.AwsSecretsManager;

public sealed class AwsSecretsManagerOptions
{
    /// <summary>
    /// Alibaba Cloud region (e.g. "eu-central-1")
    /// </summary>
    public string Region { get; set; } = "";
    /// <summary>
    /// Access key ID. When null, the default credential chain is used
    /// </summary>
    public string? AccessKey { get; set; }
    /// <summary>
    /// Secret access key
    /// </summary>
    public string? SecretKey { get; set; }
    /// <summary>
    /// Optional session token for temporary credentials
    /// </summary>
    public string? SessionToken { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
    }
}
