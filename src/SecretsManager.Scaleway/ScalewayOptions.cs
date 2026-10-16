namespace SecretsManager.Scaleway;

/// <summary>
/// Configuration options for the Scaleway secret provider.
/// </summary>
public sealed class ScalewayOptions
{
    /// <summary>
    /// Scaleway region (e.g. "fr-par", "nl-ams", "pl-waw").
    /// </summary>
    public string Region { get; set; } = "";

    /// <summary>
    /// Scaleway project UUID.
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Scaleway access key (e.g. "SCWXXXXXXXXXXXXXXXXX").
    /// </summary>
    public string AccessKey { get; set; } = "";

    /// <summary>
    /// Scaleway secret key (used as X-Auth-Token header value).
    /// </summary>
    public string SecretKey { get; set; } = "";

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// An empty collection indicates the options are valid.
    /// Checks that <see cref="Region"/>, <see cref="ProjectId"/>, <see cref="AccessKey"/>,
    /// and <see cref="SecretKey"/> are non-empty.
    /// </summary>
    /// <returns>Validation error messages; empty when the options are valid.</returns>
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
