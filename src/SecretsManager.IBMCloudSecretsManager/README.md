# SecretsManager.IBMCloudSecretsManager

IBM Cloud Secrets Manager provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithIBMCloudSecretsManager(options =>
    {
        options.Region = "eu-de";
        options.InstanceId = "your-instance-id";
        options.ApiKey = "your-api-key";
    });
```

## Installation

```bash
dotnet add package SecretsManager.IBMCloudSecretsManager
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
