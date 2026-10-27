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

Options can also be supplied from environment variables (`SECRETS_INFISICAL_*`), YAML, or
`IConfiguration` via `ConfigureInfisical`. See the
[Infisical provider documentation](https://mrhamburg.github.io/SecretsManager/docs/providers/infisical)
for the full options reference and behavior notes.

## Installation

```bash
dotnet add package SecretsManager.Infisical
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
