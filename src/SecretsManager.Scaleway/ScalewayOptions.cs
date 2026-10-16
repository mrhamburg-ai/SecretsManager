namespace SecretsManager.Scaleway;

public sealed class ScalewayOptions
{
    public string Region { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string AccessKey { get; set; } = "";
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
