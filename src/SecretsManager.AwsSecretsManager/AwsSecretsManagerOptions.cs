namespace SecretsManager.AwsSecretsManager;

public sealed class AwsSecretsManagerOptions
{
    public string Region { get; set; } = "";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? SessionToken { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
    }
}
