namespace SecretsManager.OracleVault;

public sealed class OracleVaultOptions
{
    public string AuthenticationType { get; set; } = "configfile";
    public string? ProfileName { get; set; } = "DEFAULT";
    public string? ConfigFilePath { get; set; }
    public string? Region { get; set; }
    public string? VaultId { get; set; }
    public string? CompartmentId { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (AuthenticationType == "configfile" && string.IsNullOrWhiteSpace(ConfigFilePath))
            yield return "ConfigFilePath is required when AuthenticationType is configfile";
    }
}
