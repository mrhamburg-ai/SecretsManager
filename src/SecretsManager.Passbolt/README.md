# mrhamburg.SecretsManager.Passbolt

Passbolt secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithPassbolt(options =>
    {
        options.BaseUrl = "https://passbolt.example.com";
        options.UserPrivateKey = "your-private-key";
        options.UserPrivateKeyPassphrase = "your-passphrase";
    });
```

## Installation

```bash
dotnet add package mrhamburg.SecretsManager.Passbolt
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
