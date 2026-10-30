# mrhamburg.SecretsManager.OVH

OVH Secrets Manager provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithOVH(options =>
    {
        options.Endpoint = "https://eu.api.ovh.com/1.0";
        options.ApplicationKey = "your-application-key";
        options.ApplicationSecret = "your-application-secret";
        options.ConsumerKey = "your-consumer-key";
    });
```

## Installation

```bash
dotnet add package mrhamburg.SecretsManager.OVH
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
