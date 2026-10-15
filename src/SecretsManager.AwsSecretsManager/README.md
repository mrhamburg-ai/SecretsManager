# SecretsManager.AwsSecretsManager

AWS Secrets Manager provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "eu-central-1";
        options.AccessKey = "your-access-key";
        options.SecretKey = "your-secret-key";
    });
```

## Installation

```bash
dotnet add package SecretsManager.AwsSecretsManager
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
