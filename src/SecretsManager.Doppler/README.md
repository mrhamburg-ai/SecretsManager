# mrhamburg.SecretsManager.Doppler

Doppler secret provider for SecretsManager.

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithDoppler(options =>
    {
        options.Token = Environment.GetEnvironmentVariable("SECRETS_DOPPLER_TOKEN")!;
        options.Project = "my-project";
        options.Config = "dev";
    });
```

Options can also be supplied from environment variables (`SECRETS_DOPPLER_*`), YAML, or
`IConfiguration` via `ConfigureDoppler`. See the
[Doppler provider documentation](https://mrhamburg.github.io/SecretsManager/docs/providers/doppler)
for the full options reference and behavior notes.

## Installation

```bash
dotnet add package mrhamburg.SecretsManager.Doppler
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
