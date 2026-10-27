# SecretsManager.Infisical

Infisical secret provider for SecretsManager.

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithInfisical(options =>
    {
        options.HostUri = "https://infisical.example.com";
        options.ClientId = "your-machine-identity-client-id";
        options.ClientSecret = "your-machine-identity-client-secret";
        options.ProjectId = "your-project-id";
        options.EnvironmentSlug = "dev";
        options.SecretPath = "/";
    });
```

## Installation

```bash
dotnet add package SecretsManager.Infisical
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
