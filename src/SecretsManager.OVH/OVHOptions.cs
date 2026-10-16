namespace SecretsManager.OVH;

public sealed class OVHOptions
{
    public string Endpoint { get; set; } = "";
    public string ApplicationKey { get; set; } = "";
    public string ApplicationSecret { get; set; } = "";
    public string ConsumerKey { get; set; } = "";
    public string OkmsId { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            yield return "Endpoint is required";
        if (string.IsNullOrWhiteSpace(ApplicationKey))
            yield return "ApplicationKey is required";
        if (string.IsNullOrWhiteSpace(ApplicationSecret))
            yield return "ApplicationSecret is required";
        if (string.IsNullOrWhiteSpace(ConsumerKey))
            yield return "ConsumerKey is required";
    }
}
