---
id: oracle-vault
title: Oracle Vault Provider
sidebar_label: Oracle Vault
sidebar_position: 7
---

# Oracle Vault Provider

<span className="provider-badge provider-oracle">Oracle</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

[![Oracle Vault integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Oracle%20Vault%29&label=oracle%20vault)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

The Oracle Vault integration test runs against a live OCI vault on `main`. A grey
`no check runs` pill means the CI secrets are not configured, so the job is skipped.

## Overview

The Oracle Vault provider integrates with Oracle Cloud Infrastructure (OCI) Vault for enterprise-grade secret management. It leverages OCI's native SDK for authentication and secret operations, supporting config file, instance principal, and security token authentication methods.

**Key Benefits:**
- Native OCI SDK integration with automatic retry and pagination
- Support for OCI's secret versioning and rotation states
- Multiple authentication methods for different deployment scenarios
- Base64-encoded secret content handling built-in

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.OracleVault
```

## Authentication

Oracle Vault supports multiple authentication methods through the OCI .NET SDK.

### Config File (Development & Local)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithOracleVault(options =>
    {
        options.Region = "us-phoenix-1";
        options.ProfileName = "DEFAULT";
        options.VaultId = "ocid1.vault.oc1..xxxxx";
        options.CompartmentId = "ocid1.compartment.oc1..xxxxx";
    })
    .Build();
```

:::tip[OCI Config File]
Ensure you have `~/.oci/config` set up with your OCI credentials. Run `oci setup config` to create it.
:::

### Instance Principal (OCI Compute)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithOracleVault(options =>
    {
        options.Region = "us-phoenix-1";
        options.AuthenticationType = "instanceprincipal";
        options.VaultId = "ocid1.vault.oc1..xxxxx";
        options.CompartmentId = "ocid1.compartment.oc1..xxxxx";
    })
    .Build();
```

### Security Token

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithOracleVault(options =>
    {
        options.Region = "us-phoenix-1";
        options.AuthenticationType = "securitytoken";
        options.ConfigFilePath = "/path/to/security-token-file";
        options.VaultId = "ocid1.vault.oc1..xxxxx";
        options.CompartmentId = "ocid1.compartment.oc1..xxxxx";
    })
    .Build();
```

## Configuration

You can configure Oracle Vault using the Fluent API, environment variables, or YAML.

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `AuthenticationType` | `string` | No | `"configfile"` | `configfile`, `instanceprincipal` or `securitytoken` |
| `ProfileName` | `string?` | No | `"DEFAULT"` | OCI config profile name. `configfile` only |
| `ConfigFilePath` | `string?` | Conditional | `null` | Path to the OCI config file; defaults to `~/.oci/config`. Required when `AuthenticationType` is `configfile` |
| `Region` | `string?` | Yes | `null` | OCI region, e.g. `us-phoenix-1` or `eu-frankfurt-1` |
| `VaultId` | `string?` | Conditional | `null` | Vault OCID. Required for `PutSecretAsync` and `DeleteSecretAsync` |
| `CompartmentId` | `string?` | Conditional | `null` | Compartment OCID. Required for `PutSecretAsync` |

`OracleVaultOptions.Validate()` requires `Region`, and `ConfigFilePath` when `AuthenticationType` is `configfile`. `VaultId` and `CompartmentId` are needed only for write operations.

### Fluent API

```csharp
var provider = new SecretProviderBuilder()
    .WithOracleVault(options =>
    {
        options.Region = "us-phoenix-1";
        options.AuthenticationType = "configfile";
        options.ProfileName = "DEFAULT";
        options.VaultId = "ocid1.vault.oc1..xxxxx";
        options.CompartmentId = "ocid1.compartment.oc1..xxxxx";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=oraclevault
export SECRETS_ORACLEVAULT_REGION=us-phoenix-1
export SECRETS_ORACLEVAULT_VAULTID=ocid1.vault.oc1..xxxxx
export SECRETS_ORACLEVAULT_COMPARTMENTID=ocid1.compartment.oc1..xxxxx
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithOracleVault()
    .FromEnvironment()
    .Build();
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  oracleVault:
    region: us-phoenix-1
    authentication:
      type: configfile
      profileName: DEFAULT
    vault:
      id: ocid1.vault.oc1..xxxxx
    compartment:
      id: ocid1.compartment.oc1..xxxxx
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureOracleVault(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "OracleVault": {
      "AuthenticationType": "configfile",
      "Region": "eu-frankfurt-1"
    }
  }
}
```

This reads from the `SecretsManager:OracleVault` section and validates all options.

## Oracle-Specific Features

### Secret Identification

Oracle Vault uses **OCIDs** (Oracle Cloud Identifiers) to reference secrets. The `key` parameter in all provider methods should be the full secret OCID:

```csharp
var secretOcid = "ocid1.vaultsecret.oc1.ap-mumbai-1.xxxxx";
var secret = await provider.GetSecretAsync(secretOcid);
```

### Secret Versioning

Oracle Vault maintains version history with numeric version numbers and named stages (CURRENT, PREVIOUS, PENDING):

```csharp
// Get all versions of a secret
var versions = await provider.GetSecretVersionsAsync(secretOcid);

foreach (var version in versions)
{
    Console.WriteLine($"Version: {version.Version}");
    Console.WriteLine($"Created: {version.CreatedAt}");
    Console.WriteLine($"Current: {version.IsCurrent}");
}

// Get a specific version by version number
var query = new SecretQuery { Version = "2" };
var oldSecret = await provider.GetSecretAsync(secretOcid, query);
```

### Secret Creation and Updates

When calling `PutSecretAsync`, the provider automatically determines whether to create a new secret or update an existing one:

- **New secret**: Requires `VaultId` and `CompartmentId` to be configured
- **Existing secret**: Updates the current version automatically

```csharp
// Create or update a secret
var result = await provider.PutSecretAsync(secretOcid, "my-secret-value");

// With metadata
var metadata = new SecretMetadata
{
    ContentType = "application/json",
    Tags = new Dictionary<string, string>
    {
        ["Environment"] = "Production",
        ["Owner"] = "Platform Team",
    }
};

await provider.PutSecretAsync(secretOcid, "{\"key\":\"value\"}", metadata);
```

### Secret Deletion

Oracle Vault uses soft-delete with scheduled deletion. When you call `DeleteSecretAsync`, the secret is marked for deletion but not immediately removed:

```csharp
await provider.DeleteSecretAsync(secretOcid);
```

:::warning[Soft Delete]
OCI Vault uses soft-delete by default. Deleted secrets can be recovered within the deletion window configured for your vault.
:::

## Best Practices

- Use Instance Principal authentication when running on OCI compute instances
- Store your OCI config file securely and never commit it to version control
- Use separate vaults for different environments (dev, staging, production)
- Configure appropriate IAM policies for your application's OCI identity
- Enable audit logging on your vault for compliance and monitoring
