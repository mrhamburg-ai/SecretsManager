---
id: google-secret-manager
title: Google Cloud Secret Manager Provider
sidebar_label: Google Cloud Secret Manager
sidebar_position: 7
---

# Google Cloud Secret Manager Provider

<span className="provider-badge provider-gcp">Google Cloud</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Google Cloud Secret Manager provider integrates with Google Cloud's fully managed secret storage service. It supports Application Default Credentials (ADC), service account keys, and emulator endpoints for local development.

**Key Benefits:**
- Native integration with Google Cloud IAM for fine-grained access control
- Automatic secret versioning and rotation
- Seamless authentication via Application Default Credentials (ADC)
- Audit logging through Cloud Audit Logs

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.GoogleSecretManager
```

## Authentication

Google Cloud Secret Manager supports multiple authentication methods. SecretsManager uses the Google Cloud client library's credential chain.

### Application Default Credentials (Recommended for Production)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "my-gcp-project";
        // ADC is used automatically (GOOGLE_APPLICATION_CREDENTIALS, GCE metadata, etc.)
    })
    .Build();
```

:::tip[Application Default Credentials]
When running on Google Cloud (Cloud Run, GCE, GKE, etc.), ADC is configured automatically. For local development, set the `GOOGLE_APPLICATION_CREDENTIALS` environment variable to point to your service account key file.
:::

### Service Account Key File

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "my-gcp-project";
        options.CredentialsPath = "/path/to/service-account-key.json";
    })
    .Build();
```

### Inline Service Account JSON

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "my-gcp-project";
        options.CredentialsJson = Environment.GetEnvironmentVariable("GCP_SA_KEY");
    })
    .Build();
```

### Emulator (Development)

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "test-project";
        options.Endpoint = "localhost:8080";
    })
    .Build();
```

## Configuration

You can configure Google Cloud Secret Manager using the Fluent API, environment variables, or YAML.

### Fluent API

```csharp
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "my-gcp-project";
        options.CredentialsPath = "/path/to/service-account-key.json";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=googlesecretmanager
export SECRETS_GOOGLESECRETMANAGER_PROJECTID=my-gcp-project
export SECRETS_GOOGLESECRETMANAGER_CREDENTIALSPATH=/path/to/service-account-key.json
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager()
    .FromEnvironment()
    .Build();
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  googleSecretManager:
    project:
      id: my-gcp-project
    credentials:
      path: /path/to/service-account-key.json
```

## Google Cloud-Specific Features

### Secret Versioning

Google Cloud Secret Manager automatically maintains version history for all secrets:

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
var query = new SecretQuery { Version = "2" };
var oldSecret = await provider.GetSecretAsync("database-password", query);
```

### Labels and Metadata

```csharp
// Put a secret with metadata
var metadata = new SecretMetadata
{
    ContentType = "text/plain",
    Tags = new Dictionary<string, string>
    {
        ["environment"] = "production",
        ["owner"] = "platform-team",
    }
};

await provider.PutSecretAsync("api-key", "sk-1234567890", metadata);

// Retrieve metadata
var secret = await provider.GetSecretAsync("api-key");
Console.WriteLine($"Content-Type: {secret.ContentType}");
```

:::warning[IAM Permissions]
Ensure your service account has the `roles/secretmanager.admin` role (or at minimum `roles/secretmanager.secretAccessor` and `roles/secretmanager.secretVersionAdder`) on the target project.
:::

## Best Practices

- Use Application Default Credentials (ADC) whenever possible to avoid credential management
- Use separate GCP projects or separate secret prefixes for different environments (dev, staging, production)
- Enable Cloud Audit Logs to track secret access patterns
- Grant the principle of least privilege — only assign the IAM roles your application actually needs
- Rotate service account keys regularly if using explicit credentials
