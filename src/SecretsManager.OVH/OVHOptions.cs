namespace SecretsManager.OVH;

public sealed class OVHOptions
{
    /// <summary>
    /// Custom API endpoint (optional)
    /// </summary>
    public string Endpoint { get; set; } = "";
    /// <summary>
    /// OVH application key
    /// </summary>
    public string ApplicationKey { get; set; } = "";
    /// <summary>
    /// OVH application secret
    /// </summary>
    public string ApplicationSecret { get; set; } = "";
    /// <summary>
    /// OVH consumer key
    /// </summary>
    public string ConsumerKey { get; set; } = "";
    /// <summary>
    /// OVH OKMS ID
    /// </summary>
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
