namespace SecretsManager.OracleVault;

public sealed class OracleVaultOptions
{
    /// <summary>
    /// Authentication type: "configfile", "instanceprincipal", "securitytoken"
    /// </summary>
    public string AuthenticationType { get; set; } = "configfile";
    /// <summary>
    /// OCI config profile name (e.g. "DEFAULT", "DEV")
    /// </summary>
    public string? ProfileName { get; set; } = "DEFAULT";
    /// <summary>
    /// Path to the OCI config file. Defaults to ~/.oci/config
    /// </summary>
    public string? ConfigFilePath { get; set; }
    /// <summary>
    /// Cloud region (e.g. "us-east-1", "eu-west-1")
    /// </summary>
    public string? Region { get; set; }
    /// <summary>
    /// The OCID of the vault to use for write operations
    /// </summary>
    public string? VaultId { get; set; }
    /// <summary>
    /// The OCID of the compartment where secrets are created
    /// </summary>
    public string? CompartmentId { get; set; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (AuthenticationType == "configfile" && string.IsNullOrWhiteSpace(ConfigFilePath))
            yield return "ConfigFilePath is required when AuthenticationType is configfile";
    }
}
