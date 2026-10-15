# SecretsManager.GoogleSecretManager

Google Cloud Secret Manager provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "your-project-id";
        options.CredentialsPath = "/path/to/credentials.json";
    });
```

## Installation

```bash
dotnet add package SecretsManager.GoogleSecretManager
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
