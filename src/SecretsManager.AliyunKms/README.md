# SecretsManager.AliyunKms

Alibaba Cloud KMS secret provider

## Usage

```csharp
var builder = new SecretProviderBuilder()
    .WithAliyunKms(options =>
    {
        options.Region = "eu-central-1";
        options.AccessKeyId = "your-access-key-id";
        options.AccessKeySecret = "your-access-key-secret";
    });
```

## Installation

```bash
dotnet add package SecretsManager.AliyunKms
```

## License

MIT — see [LICENSE](https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE)
