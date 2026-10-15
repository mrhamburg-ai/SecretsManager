# SecretsManager.TencentCloud

Tencent Cloud secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithTencentCloud(options =>
    {
        options.Region = "ap-guangzhou";
        options.SecretId = "your-secret-id";
        options.SecretKey = "your-secret-key";
    });
```

## Installation

```bash
dotnet add package SecretsManager.TencentCloud
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
