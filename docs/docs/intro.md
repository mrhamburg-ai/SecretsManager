---
id: intro
title: Introduction
sidebar_label: Introduction
sidebar_position: 1
slug: /intro
---

# Introduction

<span className="badge-pill badge-dotnet">.NET 10.0</span>

## What is SecretsManager?

SecretsManager is a unified .NET library that provides a consistent interface for managing secrets across multiple backend providers. Inspired by Kubernetes External Secrets Operator (ESO), it abstracts away the complexity of different secret storage solutions while maintaining full feature compatibility.

Whether you're storing secrets in Azure Key Vault for production, Scaleway Secret Manager for European compliance, or encrypted local files for development, SecretsManager gives you a single API to work with.

:::tip[Why Unified?]
By using a single interface (`ISecretProvider`), you can swap providers without changing your business logic. This is especially useful for multi-cloud deployments or migrating between providers.
:::

## Key Features

- **Unified API** -- One interface for all providers with `Get`, `Put`, `Delete`, and `Version` operations
- **Multi-Provider Support** -- FileSystem, Azure Key Vault, Scaleway Secret Manager, and PostgreSQL
- **Modular Design** -- Install only the NuGet packages you need
- **Versioning** -- First-class support for secret history across all providers
- **JSON Extraction** -- Query nested JSON secrets using dot-path syntax
- **Flexible Configuration** -- Choose from Fluent API, Environment Variables, or YAML
- **Layering** -- Add cross-cutting behaviors like retries, timeouts, and telemetry without changing provider code

## Installation

Install the core package and your chosen provider(s):

```bash title="Terminal"
# Core library
dotnet add package SecretsManager

# Choose your provider(s)
dotnet add package SecretsManager.FileSystem
dotnet add package SecretsManager.AzureKeyVault
dotnet add package SecretsManager.Scaleway
dotnet add package SecretsManager.PostgreSql
```

## Quick Example

Here's how to get started with the FileSystem provider:

```csharp title="Program.cs"
using SecretsManager;

// Configure the provider
var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/etc/secrets";
        options.EncryptionKey = "your-encryption-key";
    })
    .Build();

// Get a secret
var secret = await provider.GetSecretAsync("database-password");
Console.WriteLine($"Password: {secret.Value}");

// Put a new secret
await provider.PutSecretAsync("api-key", "sk-1234567890");

// Get secret with version history
var versions = await provider.GetSecretVersionsAsync("api-key");
foreach (var version in versions)
{
    Console.WriteLine($"Version {version.Version}: {version.CreatedAt}");
}
```

:::info[Next Steps]
Continue to the [Quick Start guide](/docs/getting-started/quick-start) to learn how to configure different providers and explore advanced features.
You can also read the [Layering docs](/docs/advanced/layering) to add built-in and custom policy layers.
:::
