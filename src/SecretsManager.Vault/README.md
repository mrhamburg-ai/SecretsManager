# SecretsManager.Vault

HashiCorp Vault secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithVault(options =>
    {
        options.Url = "https://vault.example.com:8200";
        options.Token = "your-vault-token";
        options.MountPath = "secret";
    });
```

## Installation

```bash
dotnet add package SecretsManager.Vault
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
