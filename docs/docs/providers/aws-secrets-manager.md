---
id: aws-secrets-manager
title: AWS Secrets Manager Provider
sidebar_label: AWS Secrets Manager
sidebar_position: 6
---

# AWS Secrets Manager Provider

<span className="provider-badge provider-aws">AWS</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The AWS Secrets Manager provider integrates with Amazon Web Services' fully managed secret management service. It supports the AWS SDK's default credential chain as well as explicit access key authentication.

**Key Benefits:**
- Seamless integration with AWS IAM and credential chain
- Automatic secret versioning and rotation support
- Fine-grained access control via IAM policies
- Multi-Region replication capabilities

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.AwsSecretsManager
```

## Authentication

AWS Secrets Manager supports multiple authentication methods. SecretsManager leverages the AWS SDK's built-in credential resolution.

### Default Credential Chain (Recommended)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "us-east-1";
        // Uses default credential chain: env vars, ~/.aws/credentials, EC2 instance profile, etc.
    })
    .Build();
```

:::tip[Default Credentials]
The AWS SDK automatically resolves credentials from environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`), the `~/.aws/credentials` file, EC2 instance profiles, or ECS task roles.
:::

### Explicit Access Keys

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "us-east-1";
        options.AccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        options.SecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
    })
    .Build();
```

### Temporary Credentials (Session Token)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "us-east-1";
        options.AccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        options.SecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        options.SessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");
    })
    .Build();
```

## Configuration

You can configure AWS Secrets Manager using the Fluent API, environment variables, or YAML.

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `Region` | `string` | Yes | `""` | AWS region, e.g. `us-east-1` or `eu-west-1` |
| `AccessKey` | `string?` | No | `null` | Explicit AWS access key ID. When `null`, the AWS default credential chain is used |
| `SecretKey` | `string?` | No | `null` | Explicit AWS secret access key. Required when `AccessKey` is set |
| `SessionToken` | `string?` | No | `null` | Session token, for temporary (STS) credentials |

`AwsSecretsManagerOptions.Validate()` requires `Region`. Credentials are optional because the AWS SDK resolves them from its default credential chain when `AccessKey`/`SecretKey` are left `null`.

### Fluent API

```csharp
var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "us-east-1";
        options.AccessKey = "AKIAIOSFODNN7EXAMPLE";
        options.SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=awssecretsmanager
export SECRETS_AWSSECRETSMANAGER_REGION=us-east-1
export SECRETS_AWSSECRETSMANAGER_ACCESSKEY=AKIAIOSFODNN7EXAMPLE
export SECRETS_AWSSECRETSMANAGER_SECRETKEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager()
    .FromEnvironment()
    .Build();
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  awsSecretsManager:
    region: us-east-1
    access:
      key: AKIAIOSFODNN7EXAMPLE
    secret:
      key: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
```

### Dependency Injection (IOptions&lt;T&gt;)

Bind the same settings from `appsettings.json` through the DI container:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureAwsSecretsManager(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "AwsSecretsManager": {
      "Region": "us-east-1"
    }
  }
}
```

This reads from the `SecretsManager:AwsSecretsManager` section and validates all options.

## AWS-Specific Features

### Secret Versioning

AWS Secrets Manager automatically maintains version history with unique version IDs:

```csharp
// Get all versions of a secret
var versions = await provider.GetSecretVersionsAsync("database-password");

foreach (var version in versions)
{
    Console.WriteLine($"Version: {version.Version}");
    Console.WriteLine($"Created: {version.CreatedAt}");
    Console.WriteLine($"Current: {version.IsCurrent}");
}

// Get a specific version
var query = new SecretQuery { Version = "abc123-def456-..." };
var oldSecret = await provider.GetSecretAsync("database-password", query);
```

### JSON Secret Extraction

AWS Secrets Manager commonly stores database credentials as JSON. Extract individual fields directly:

```csharp
// Store a JSON secret
await provider.PutSecretAsync(
    "database-credentials",
    """{"host":"db.example.com","port":5432,"username":"admin","password":"secret"}""",
    new SecretMetadata { ContentType = "application/json" });

// Extract a single field
var query = new SecretQuery { Property = "host" };
var host = await provider.GetSecretAsync("database-credentials", query);
Console.WriteLine(host.Value); // "db.example.com"

// Extract a nested field
var nestedQuery = new SecretQuery { Property = "credentials.primary.host" };
var nestedHost = await provider.GetSecretAsync("deep-config", nestedQuery);
```

:::warning[IAM Permissions]
Ensure your IAM identity has `secretsmanager:GetSecretValue`, `secretsmanager:PutSecretValue`, `secretsmanager:DeleteSecret`, and `secretsmanager:ListSecretVersionIds` permissions for the secrets you need to access.
:::

## Best Practices

- Use IAM roles (instance profiles, task roles) instead of long-lived access keys in production
- Enable automatic rotation for database credentials and API keys
- Use separate secrets for each environment (dev, staging, production)
- Tag secrets with environment and owner metadata for easier management
- Monitor secret access via AWS CloudTrail for audit compliance
