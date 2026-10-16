namespace SecretsManager.AzureKeyVault;

public sealed class AzureKeyVaultOptions
{
    public string VaultUrl { get; set; } = "";
    public string AuthenticationType { get; set; } = "default";
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
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
