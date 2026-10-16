---
id: conjur
title: CyberArk Conjur Provider
sidebar_label: CyberArk Conjur
sidebar_position: 16
---

# CyberArk Conjur Provider

<span className="provider-badge provider-conjur">CyberArk Conjur</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Conjur provider integrates with [CyberArk Conjur](https://www.cyberark.com/conjur/), an open-source secrets management solution for machine identity and SSH access. It communicates via Conjur's REST API using `HttpClient` — no SDK dependency required.

**Key Benefits:**
- Open-source, self-hosted, and widely adopted for DevOps/CI pipelines
- Continuous delivery of secrets with versioning built in
- Fine-grained access control via policy-based roles and privileges
- Machine identity focused (hosts, apps, CI/CD platforms)

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.Conjur
```

## Configuration

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithConjur(options =>
    {
        options.Url = "https://conjur.example.com:443";
        options.Account = "myorg";
        options.Login = "host/ci-app";
        options.ApiKey = "your-api-key";
        options.PolicyPath = "root";
        options.SkipTlsVerify = false;
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=conjur
export SECRETS_CONJUR_URL=https://conjur.example.com:443
export SECRETS_CONJUR_ACCOUNT=myorg
export SECRETS_CONJUR_LOGIN=host/ci-app
export SECRETS_CONJUR_APIKEY=your-api-key
export SECRETS_CONJUR_POLICYPATH=root
export SECRETS_CONJUR_SKIPTLSVERIFY=false
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  conjur:
    url: https://conjur.example.com:443
    account: myorg
    login: host/ci-app
    apiKey: your-api-key
    policy:
      path: root
    skip:
      tls:
        verify: false
```

## Authentication

Conjur authenticates a **user** or **host** with its **API key** to obtain a short-lived access token. You'll need:

1. A **Conjur URL** (default: `https://localhost:443`)
2. An **account** name configured on the server
3. A **login** (for a host, prefix the host id with `host/`)
4. An **API key** (a user's password works for a user login)

The provider uses the token only for its TTL (parsed from the JWT) and refreshes it automatically before it expires, so long-running applications never re-authenticate manually.

:::tip
API keys are obtained from the Conjur UI (`Provide API Key` / `API Key` sections) or by authenticating with a password. Treat API keys like secrets — use a policy that grants the least privileges required.
:::

## Local Development

Conjur provides an official Docker image. Quick start with a local Postgres:

```bash title="Terminal"
docker run -d --name conjur-db \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres \
  -e POSTGRES_DB=conjur postgres:16

docker run -d --name conjur \
  -e DATABASE_URL=postgres://postgres:postgres@conjur-db:5432/conjur \
  -e CONJUR_DATA_KEY=$(openssl rand -base64 32) \
  -p 8080:80 cyberark/conjur:latest \
  server -a myorg
```

The admin API key is printed to the container logs on first startup (`API key for admin: ...`).

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

## Behavior Notes

- **Writing secrets:** Conjur requires a variable to exist in policy before a value can be set. The provider checks for the variable and, if missing, loads a `!variable` policy entry into the configured policy path automatically. Set `PolicyPath` to the branch that owns your variables (default `root`).
- **Reading secrets:** Secrets are retrieved by their full identifier (e.g. `app/db-pass`). Conjur keeps up to 20 versions per variable.
- **Deleting secrets:** Conjur has no REST delete endpoint. Deletion is performed by patching the configured policy path with a `!delete` statement, which requires the `update` privilege on the containing policy.
- **Nested variables:** For variables declared inside a policy branch, set `PolicyPath` to that branch; the leading branch segment is stripped when the provider creates missing variables.

### Configuration via IOptions<T>

You can also bind configuration from `IConfiguration`:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureConjur(configuration)
    .Build();
```

This reads from the `SecretsManager:Conjur` section and validates all options.
