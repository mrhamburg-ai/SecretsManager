# mrhamburg.SecretsManager.PostgreSql

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

## Audit sink

Write secret access audit records to PostgreSQL:

```csharp
var builder = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=postgres;";
    })
    .WithPostgreSqlAuditSink(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=postgres;";
    });
```

## Installation

```bash
dotnet add package mrhamburg.SecretsManager.PostgreSql
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
