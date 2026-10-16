namespace SecretsManager.AzureKeyVault;

public sealed class AzureKeyVaultOptions
{
    /// <summary>
    /// Base URL of the service endpoint
    /// </summary>
    public string VaultUrl { get; set; } = "";
    /// <summary>
    /// Authentication type: "configfile", "instanceprincipal", "securitytoken"
    /// </summary>
    public string AuthenticationType { get; set; } = "default";
    /// <summary>
    /// Azure AD tenant ID. Required for service principal authentication
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Azure AD application (client) ID
    /// </summary>
    public string? ClientId { get; set; }
    /// <summary>
    /// Azure AD client secret. Required for service principal authentication
    /// </summary>
    public string? ClientSecret { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(VaultUrl))
            yield return "VaultUrl is required";
        if (!Uri.TryCreate(VaultUrl, UriKind.Absolute, out _))
            yield return $"VaultUrl '{VaultUrl}' is not a valid absolute URI";
        if (AuthenticationType == "serviceprincipal")
        {
            if (string.IsNullOrWhiteSpace(TenantId))
                yield return "TenantId is required for service principal authentication";
            if (string.IsNullOrWhiteSpace(ClientId))
                yield return "ClientId is required for service principal authentication";
            if (string.IsNullOrWhiteSpace(ClientSecret))
                yield return "ClientSecret is required for service principal authentication";
        }
    }
}
