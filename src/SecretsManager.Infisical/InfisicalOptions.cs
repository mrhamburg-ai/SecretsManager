namespace SecretsManager.Infisical;

/// <summary>
/// Options for the Infisical secret provider.
/// </summary>
public sealed class InfisicalOptions
{
    /// <summary>
    /// Base URL of the Infisical instance. Defaults to <c>https://app.infisical.com</c>.
    /// Set this to the URL of a self-hosted instance when applicable.
    /// </summary>
    public string HostUri { get; set; } = "https://app.infisical.com";

    /// <summary>
    /// Client ID of the machine identity that uses Universal Auth.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Client secret of the machine identity that uses Universal Auth.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// ID of the Infisical project that contains the secrets.
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Slug of the environment that contains the secrets. Defaults to <c>dev</c>.
    /// </summary>
    public string EnvironmentSlug { get; set; } = "dev";

    /// <summary>
    /// Secret path that contains the secrets. Defaults to <c>/</c>.
    /// </summary>
    public string SecretPath { get; set; } = "/";

    /// <summary>
    /// Returns the validation errors for the current options; an empty sequence means the options
    /// are valid. Requires <see cref="HostUri"/>, <see cref="ClientId"/>,
    /// <see cref="ClientSecret"/>, <see cref="ProjectId"/>, <see cref="EnvironmentSlug"/> and
    /// <see cref="SecretPath"/> to be set.
    /// </summary>
    /// <returns>One message per invalid setting.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(HostUri))
            yield return "HostUri is required";
        if (string.IsNullOrWhiteSpace(ClientId))
            yield return "ClientId is required";
        if (string.IsNullOrWhiteSpace(ClientSecret))
            yield return "ClientSecret is required";
        if (string.IsNullOrWhiteSpace(ProjectId))
            yield return "ProjectId is required";
        if (string.IsNullOrWhiteSpace(EnvironmentSlug))
            yield return "EnvironmentSlug is required";
        if (string.IsNullOrWhiteSpace(SecretPath))
            yield return "SecretPath is required";
    }
}
