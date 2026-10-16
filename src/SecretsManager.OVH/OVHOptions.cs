namespace SecretsManager.OVH;

/// <summary>
/// Configuration options for the OVHcloud Secrets Management provider.
/// </summary>
public sealed class OVHOptions
{
    /// <summary>
    /// OVH endpoint region (e.g. "ovh-eu", "ovh-us", "ovh-ca").
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// OVH application key.
    /// </summary>
    public string ApplicationKey { get; set; } = "";

    /// <summary>
    /// OVH application secret.
    /// </summary>
    public string ApplicationSecret { get; set; } = "";

    /// <summary>
    /// OVH consumer key.
    /// </summary>
    public string ConsumerKey { get; set; } = "";

    /// <summary>
    /// OVH OKMS ID.
    /// </summary>
    public string OkmsId { get; set; } = "";

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// </summary>
    /// <returns>An enumerable of validation error messages. An empty collection indicates the options are valid.</returns>
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
