namespace SecretsManager.OracleVault;

/// <summary>
/// Configuration options for the Oracle Vault secret provider.
/// </summary>
public sealed class OracleVaultOptions
{
    /// <summary>
    /// Authentication type: "configfile", "instanceprincipal", "securitytoken".
    /// Defaults to "configfile" (reads from ~/.oci/config).
    /// </summary>
    public string AuthenticationType { get; set; } = "configfile";

    /// <summary>
    /// OCI config profile name (e.g., "DEFAULT", "DEV", "PROD").
    /// Only used when AuthenticationType is "configfile".
    /// </summary>
    public string? ProfileName { get; set; } = "DEFAULT";

    /// <summary>
    /// Path to the OCI config file. Defaults to ~/.oci/config.
    /// Only used when AuthenticationType is "configfile".
    /// </summary>
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// OCI region (e.g., "us-phoenix-1", "eu-frankfurt-1").
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// The OCID of the vault to use for write operations (create/update/delete).
    /// Required for PutSecretAsync and DeleteSecretAsync.
    /// </summary>
    public string? VaultId { get; set; }

    /// <summary>
    /// The OCID of the compartment where secrets are created.
    /// Required for PutSecretAsync.
    /// </summary>
    public string? CompartmentId { get; set; }

    /// <summary>
    /// Validates the options and returns any error messages.
    /// </summary>
    /// <returns>An error message for each invalid setting; empty when the configuration is valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
        if (AuthenticationType == "configfile" && string.IsNullOrWhiteSpace(ConfigFilePath))
            yield return "ConfigFilePath is required when AuthenticationType is configfile";
    }
}
