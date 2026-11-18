namespace SecretsManager.Doppler;

/// <summary>
/// Options for the Doppler secret provider.
/// </summary>
public sealed class DopplerOptions
{
    /// <summary>
    /// Base URL of the Doppler API. Defaults to <c>https://api.doppler.com</c>. Set this only when
    /// a proxy or a test double fronts the API.
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.doppler.com";

    /// <summary>
    /// Doppler API token. A service token (<c>dp.st.*</c>) is scoped to one project and config. A
    /// personal or service account token needs <see cref="Project"/> and <see cref="Config"/>.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// Name of the Doppler project that contains the secrets. Required unless <see cref="Token"/> is
    /// a service token.
    /// </summary>
    public string Project { get; set; } = "";

    /// <summary>
    /// Name of the Doppler config that contains the secrets, for example <c>dev</c> or
    /// <c>prd</c>. Required unless <see cref="Token"/> is a service token.
    /// </summary>
    public string Config { get; set; } = "";

    /// <summary>
    /// When <see langword="false"/>, reads return Doppler's computed value, with references such as
    /// <c>${SECRET_NAME}</c> resolved. When <see langword="true"/>, reads return the stored raw
    /// value. Defaults to <see langword="false"/>.
    /// </summary>
    public bool UseRawValues { get; set; }

    /// <summary>
    /// Returns the validation errors for the current options; an empty sequence means the options
    /// are valid. Requires <see cref="ApiUrl"/> and <see cref="Token"/>. Also requires
    /// <see cref="Project"/> and <see cref="Config"/> when <see cref="Token"/> is not a service
    /// token.
    /// </summary>
    /// <returns>One message per invalid setting.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiUrl))
            yield return "ApiUrl is required";
        else if (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out _))
            yield return "ApiUrl must be an absolute URI";

        if (string.IsNullOrWhiteSpace(Token))
            yield return "Token is required";

        if (!string.IsNullOrWhiteSpace(Token) && !IsServiceToken(Token))
        {
            if (string.IsNullOrWhiteSpace(Project))
                yield return "Project is required when Token is not a service token";
            if (string.IsNullOrWhiteSpace(Config))
                yield return "Config is required when Token is not a service token";
        }
    }

    internal static bool IsServiceToken(string token) =>
        token.StartsWith("dp.st.", StringComparison.Ordinal);
}
