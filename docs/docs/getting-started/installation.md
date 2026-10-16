---
id: installation
title: Installation
sidebar_label: Installation
sidebar_position: 3
---

# Installation

SecretsManager uses a modular architecture -- install only the packages you need.

## Core Package

The core package contains the `ISecretProvider` interface, builder, and shared types:

```bash title="Terminal"
dotnet add package SecretsManager
```

## Provider Packages

Install one or more provider packages depending on your backend:

### FileSystem Provider

Local encrypted storage using AES-256-GCM. Great for development and air-gapped environments.

```bash title="Terminal"
dotnet add package SecretsManager.FileSystem
```

### Azure Key Vault Provider

Enterprise-grade secret management with Microsoft Azure.

```bash title="Terminal"
dotnet add package SecretsManager.AzureKeyVault
```

### Scaleway Provider

European cloud provider with GDPR-compliant storage.

```bash title="Terminal"
dotnet add package SecretsManager.Scaleway
```

### PostgreSQL Provider

Self-hosted secret storage with optional AES-256-GCM encryption at rest.

```bash title="Terminal"
dotnet add package SecretsManager.PostgreSql
```

### AWS Secrets Manager Provider

Fully managed secret management with IAM integration and automatic rotation.

```bash title="Terminal"
dotnet add package SecretsManager.AwsSecretsManager
```

## Requirements

- **.NET 10.0** or later
- Each provider may have additional requirements (e.g., Azure subscription for Azure Key Vault)

:::info[Modular Design]
Provider packages are independent -- installing `SecretsManager.AzureKeyVault` does not pull in Scaleway or FileSystem dependencies. This keeps your application lean.
:::

## Verify Installation

After installing, verify the packages are available:

```csharp
using SecretsManager;
using SecretsManager.FileSystem;
using Microsoft.Extensions.DependencyInjection;

// Fluent API
ISecretProvider provider = new SecretProviderBuilder()
    .WithFileSystem(options => options.BasePath = "/tmp/secrets")
    .Build();

// Or with IOptions<T>
ISecretProvider provider2 = new SecretProviderBuilder()
    .ConfigureFileSystem(configuration)
    .Build();
```
