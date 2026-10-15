# SecretsManager.PostgreSql

PostgreSQL secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=postgres;";
        options.Schema = "public";
        options.TablePrefix = "secrets_";
    });
```

## Installation

```bash
dotnet add package SecretsManager.PostgreSql
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
