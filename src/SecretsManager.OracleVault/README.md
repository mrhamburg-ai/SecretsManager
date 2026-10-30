# mrhamburg.SecretsManager.OracleVault

Oracle Vault secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithOracleVault(options =>
    {
        options.Region = "eu-frankfurt-1";
        options.VaultId = "your-vault-id";
        options.CompartmentId = "your-compartment-id";
    });
```

## Installation

```bash
dotnet add package mrhamburg.SecretsManager.OracleVault
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
