namespace SecretsManager.AzureKeyVault;

/// <summary>
/// Options for the Azure Key Vault secret provider.
/// </summary>
public sealed class AzureKeyVaultOptions
{
    /// <summary>
    /// The Azure Key Vault URI (e.g. "https://myvault.vault.azure.net").
    /// </summary>
    public string VaultUrl { get; set; } = "";

    /// <summary>
    /// Authentication type: "default", "serviceprincipal", "managedidentity", "workloadidentity".
    /// Defaults to "default" (DefaultAzureCredential).
    /// </summary>
    public string AuthenticationType { get; set; } = "default";

    /// <summary>
    /// Azure AD tenant ID. Required for service principal authentication.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure AD application (client) ID.
    /// Required for service principal; optional for managed identity (user-assigned).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure AD client secret. Required for service principal authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Returns the validation errors for the current options; an empty sequence means the options
    /// are valid. Requires <see cref="VaultUrl"/> to be an absolute URI, and additionally requires
    /// <see cref="TenantId"/>, <see cref="ClientId"/> and <see cref="ClientSecret"/> when
    /// <see cref="AuthenticationType"/> is <c>serviceprincipal</c>.
    /// </summary>
    /// <returns>One message per invalid setting.</returns>
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
