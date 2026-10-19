---
id: azure-key-vault
title: Azure Key Vault Provider
sidebar_label: Azure Key Vault
sidebar_position: 3
---

# Azure Key Vault Provider

<span className="provider-badge provider-azure">Azure</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Azure Key Vault provider integrates with Microsoft Azure's enterprise-grade secret management service. It supports all Azure authentication methods, HSM-backed secrets, and advanced access control policies.

**Key Benefits:**
- Seamless integration with Azure Active Directory
- Hardware Security Module (HSM) support for FIPS 140-2 Level 2 compliance
- Built-in audit logging and monitoring
- Automatic secret rotation capabilities

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.AzureKeyVault
```

## Authentication

Azure Key Vault supports multiple authentication methods. SecretsManager automatically uses the Azure Identity library's credential chain.

### Managed Identity (Recommended for Production)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAzureKeyVault(options =>
    {
        options.VaultUrl = "https://my-vault.vault.azure.net";
        // Managed Identity is used automatically
    })
    .Build();
```

:::tip[Managed Identity]
When running in Azure (App Service, Container Apps, VMs, etc.), Managed Identity is the most secure option. No credentials need to be stored in your application.
:::

### Service Principal

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAzureKeyVault(options =>
    {
        options.VaultUrl = "https://my-vault.vault.azure.net";
        options.AuthenticationType = "serviceprincipal";
        options.TenantId = "your-tenant-id";
        options.ClientId = "your-client-id";
        options.ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
    })
    .Build();
```

### Azure CLI (Development)

```csharp title="Program.cs"
// No additional configuration needed
// Just run: az login
var provider = new SecretProviderBuilder()
    .WithAzureKeyVault(options =>
    {
        options.VaultUrl = "https://my-vault.vault.azure.net";
    })
    .Build();
```

## Configuration

You can configure Azure Key Vault using the Fluent API, environment variables, or YAML.

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `VaultUrl` | `string` | Yes | `""` | Key Vault URI, e.g. `https://myvault.vault.azure.net` |
| `AuthenticationType` | `string` | No | `"default"` | `default`, `serviceprincipal`, `managedidentity` or `workloadidentity` |
| `TenantId` | `string?` | Conditional | `null` | Azure AD tenant ID — required when `AuthenticationType` is `serviceprincipal` |
| `ClientId` | `string?` | Conditional | `null` | Azure AD application (client) ID — required for service principal, optional for user-assigned managed identity |
| `ClientSecret` | `string?` | Conditional | `null` | Azure AD client secret — required when `AuthenticationType` is `serviceprincipal` |

`AzureKeyVaultOptions.Validate()` requires `VaultUrl`. `TenantId`, `ClientId` and `ClientSecret` are validated only when `AuthenticationType` is `serviceprincipal`.

### Fluent API

```csharp
var provider = new SecretProviderBuilder()
    .WithAzureKeyVault(options =>
    {
        options.VaultUrl = "https://my-vault.vault.azure.net";
        options.AuthenticationType = "serviceprincipal";
        options.TenantId = "your-tenant-id";
        options.ClientId = "your-client-id";
        options.ClientSecret = "your-client-secret";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=azurekeyvault
export SECRETS_AZUREKEYVAULT_VAULTURL=https://my-vault.vault.azure.net
export SECRETS_AZUREKEYVAULT_TENANTID=your-tenant-id
export SECRETS_AZUREKEYVAULT_CLIENTID=your-client-id
export SECRETS_AZUREKEYVAULT_CLIENTSECRET=your-client-secret
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAzureKeyVault()
    .FromEnvironment()
    .Build();
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  azureKeyVault:
    vaultUrl: https://my-vault.vault.azure.net
    authentication:
      type: serviceprincipal
      tenantId: your-tenant-id
      clientId: your-client-id
      clientSecret: your-client-secret
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureAzureKeyVault(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "AzureKeyVault": {
      "VaultUrl": "https://myvault.vault.azure.net",
      "AuthenticationType": "default"
    }
  }
}
```

This reads from the `SecretsManager:AzureKeyVault` section and validates all options.

## Azure-Specific Features

### Secret Versioning

Azure Key Vault automatically maintains version history for all secrets:

```csharp
// Get all versions of a secret
var versions = await provider.GetSecretVersionsAsync("database-password");

foreach (var version in versions)
{
    Console.WriteLine($"Version: {version.Version}");
    Console.WriteLine($"Created: {version.CreatedAt}");
}

// Get a specific version
var query = new SecretQuery { Version = "abc123def456" };
var oldSecret = await provider.GetSecretAsync("database-password", query);
```

### Tags and Metadata

```csharp
// Put a secret with metadata
var metadata = new SecretMetadata
{
    ContentType = "text/plain",
    Tags = new Dictionary<string, string>
    {
        ["Environment"] = "Production",
        ["Owner"] = "Platform Team",
    }
};

await provider.PutSecretAsync("api-key", "sk-1234567890", metadata);

// Retrieve metadata
var secret = await provider.GetSecretAsync("api-key");
Console.WriteLine($"Content-Type: {secret.ContentType}");
```

:::warning[Access Policies]
Ensure your application's identity (Managed Identity or Service Principal) has the `Get`, `Set`, and `List` permissions in your Key Vault's access policies.
:::

## Best Practices

- Use Managed Identity whenever possible to avoid credential management
- Enable soft-delete and purge protection on your Key Vault for disaster recovery
- Configure Azure Monitor alerts for secret access patterns
- Use separate Key Vaults for different environments (dev, staging, production)
