# SecretsManager.FileSystem

File system secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/path/to/secrets";
    });
```

## Installation

```bash
dotnet add package SecretsManager.FileSystem
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
