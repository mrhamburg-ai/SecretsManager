---
id: vault
title: HashiCorp Vault Provider
sidebar_label: HashiCorp Vault
sidebar_position: 15
---

# HashiCorp Vault Provider

<span className="provider-badge provider-vault">HashiCorp Vault</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Vault provider integrates with HashiCorp Vault KV (Key-Value) v2 secrets engine. It communicates via Vault's REST API using `HttpClient` -- no SDK dependency required.

**Key Benefits:**
- Industry-standard secret management solution
- Built-in versioning with the KV v2 engine
- Fine-grained access control policies
- Robust audit logging
- Dynamic secrets and leasing support

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.Vault
```

## Configuration

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithVault(options =>
    {
        options.Url = "http://localhost:8200";
        options.Token = "hvs.your-vault-token";
        options.MountPath = "secret";
        options.SkipTlsVerify = false;
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=vault
export SECRETS_VAULT_URL=http://localhost:8200
export SECRETS_VAULT_TOKEN=hvs.your-vault-token
export SECRETS_VAULT_MOUNTPATH=secret
export SECRETS_VAULT_SKIPTLSVERIFY=false
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  vault:
    url: http://localhost:8200
    token: hvs.your-vault-token
    mount:
      path: secret
    skip:
      tls:
        verify: false
```

## Authentication

Vault uses token-based authentication. You'll need:

1. A **Vault Server URL** (default: `http://localhost:8200`)
2. A **Vault Token** with read/write access to the KV v2 secrets engine
3. A **Mount Path** (default: `secret`)

In development, you can start Vault in dev mode:

```bash title="Terminal"
vault server -dev -dev-root-token-id=root-token
```

This starts Vault at `http://localhost:8200` with the KV v2 engine enabled at `secret/` and the root token set to `root-token`.

:::tip
For production, always use TLS and avoid using root tokens. Create a policy with least-privilege access and generate a token from that policy.
:::

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

### Configuration via IOptions<T>

You can also bind configuration from `IConfiguration`:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureVault(configuration)
    .Build();
```

This reads from the `SecretsManager:Vault` section and validates all options.
