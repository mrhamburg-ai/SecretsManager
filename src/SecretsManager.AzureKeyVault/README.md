# SecretsManager.AzureKeyVault

Azure Key Vault secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithAzureKeyVault(options =>
    {
        options.VaultUrl = "https://your-vault.vault.azure.net/";
        options.TenantId = "your-tenant-id";
        options.ClientId = "your-client-id";
        options.ClientSecret = "your-client-secret";
    });
```

## Installation

```bash
dotnet add package SecretsManager.AzureKeyVault
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
