# mrhamburg.SecretsManager.Conjur

Conjur secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithConjur(options =>
    {
        options.Url = "https://conjur.example.com";
        options.Account = "your-account";
        options.Login = "your-login";
        options.ApiKey = "your-api-key";
    });
```

## Installation

```bash
dotnet add package mrhamburg.SecretsManager.Conjur
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
