# SecretsManager.Scaleway

Scaleway secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithScaleway(options =>
    {
        options.Region = "fr-par";
        options.ProjectId = "your-project-id";
        options.AccessKey = "your-access-key";
        options.SecretKey = "your-secret-key";
    });
```

## Installation

```bash
dotnet add package SecretsManager.Scaleway
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
