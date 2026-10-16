---
id: configuration
title: Configuration
sidebar_label: Configuration
sidebar_position: 2
---

# Configuration

SecretsManager supports three configuration approaches. Choose the one that best fits your workflow.

## Fluent API

The most common approach for .NET applications. Configure providers directly in code:

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/etc/secrets";
        options.EncryptionKey = "base64-encoded-key";
    })
    .Build();
```

This approach works well for:
- Applications with static configuration
- Dependency injection setups
- Unit testing with mock providers


## Dependency Injection (IOptions&lt;T&gt;)

For applications using `Microsoft.Extensions.DependencyInjection`, register SecretsManager and bind configuration from `IConfiguration`:

```csharp title="Program.cs"
using SecretsManager;
using SecretsManager.FileSystem;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register the SecretsManager builder
builder.Services.AddSecretsManager();

// Bind configuration from appsettings.json
builder.Services.Configure<FileSystemOptions>(
    builder.Configuration.GetSection("SecretsManager:FileSystem"));
```

Or use the `ConfigureXxx(IConfiguration)` bridge for a shorter form:

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .ConfigureFileSystem(builder.Configuration)
    .Build();
```

Configuration file (`appsettings.json`):

```json
{
  "SecretsManager": {
    "FileSystem": {
      "BasePath": "/etc/secrets",
      "EncryptionKey": "base64-encoded-key"
    }
  }
}
```

All options are validated at configuration time. Invalid values throw `ArgumentException` with a descriptive message.

This approach works well for:
- ASP.NET Core applications
- Applications using `appsettings.json` with environment-specific overrides
- Teams that want configuration validation at startup

## Environment Variables

Configure providers through environment variables for container and cloud-native deployments:

```bash title="Terminal"
export SECRETS_PROVIDER=filesystem
export SECRETS_FILESYSTEM_PATH=/etc/secrets
export SECRETS_FILESYSTEM_ENCRYPTION_KEY=base64encodedkey
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithFileSystem()
    .FromEnvironment()
    .Build();
```

This approach works well for:
- Docker and Kubernetes deployments
- CI/CD pipelines
- Cloud-native applications

## YAML Configuration

Use External Secrets Operator-style YAML files for declarative configuration:

```yaml title="secretstore.yaml"
provider:
  filesystem:
    path: /etc/secrets
    encryption:
      key: base64encodedkey
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithFileSystem()
    .FromYamlFile("secretstore.yaml")
    .Build();
```

This approach works well for:
- Teams already using External Secrets Operator
- GitOps workflows
- Multi-environment configuration management

:::tip[Combining Sources]
You can combine configuration sources. For example, use YAML for base configuration and environment variables for sensitive values like encryption keys.
:::

## Provider-Specific Configuration

Each provider has its own set of configuration options. See the individual provider documentation for details:

- [FileSystem Provider](/docs/providers/filesystem)
- [Azure Key Vault Provider](/docs/providers/azure-key-vault)
- [Scaleway Provider](/docs/providers/scaleway)
- [PostgreSQL Provider](/docs/providers/postgresql)
- [OVH Provider](/docs/providers/ovh)
- [AWS Secrets Manager Provider](/docs/providers/aws-secrets-manager)
- [HashiCorp Vault Provider](/docs/providers/vault)
- [CyberArk Conjur Provider](/docs/providers/conjur)
