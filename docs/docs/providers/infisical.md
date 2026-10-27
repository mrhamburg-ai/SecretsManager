---
id: infisical
title: Infisical Provider
sidebar_label: Infisical
sidebar_position: 17
---

# Infisical Provider

<span className="provider-badge provider-infisical">Infisical</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Infisical provider integrates with [Infisical](https://infisical.com/), an open-source
secrets management platform. It uses the official
[`Infisical.Sdk`](https://www.nuget.org/packages/Infisical.Sdk) package and authenticates with a
machine identity through Universal Auth.

**Key benefits:**

- Works with Infisical Cloud and self-hosted instances
- Open-source and self-hostable
- Machine identity authentication with scoped project access
- Secret version history
- Per-secret metadata

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.Infisical
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `HostUri` | `string` | No | `"https://app.infisical.com"` | Base URL of the Infisical instance. Set this for self-hosted instances |
| `ClientId` | `string` | Yes | `""` | Client ID of the machine identity that uses Universal Auth |
| `ClientSecret` | `string` | Yes | `""` | Client secret of the machine identity that uses Universal Auth |
| `ProjectId` | `string` | Yes | `""` | ID of the Infisical project that contains the secrets |
| `EnvironmentSlug` | `string` | No | `"dev"` | Slug of the environment that contains the secrets |
| `SecretPath` | `string` | No | `"/"` | Secret path that contains the secrets |

`InfisicalOptions.Validate()` requires `HostUri`, `ClientId`, `ClientSecret`, `ProjectId`,
`EnvironmentSlug` and `SecretPath`. The default values satisfy the `HostUri`, `EnvironmentSlug` and
`SecretPath` requirements.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithInfisical(options =>
    {
        options.HostUri = "https://infisical.example.com";
        options.ClientId = "your-machine-identity-client-id";
        options.ClientSecret = "your-machine-identity-client-secret";
        options.ProjectId = "your-project-id";
        options.EnvironmentSlug = "dev";
        options.SecretPath = "/";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=infisical
export SECRETS_INFISICAL_HOST_URI=https://infisical.example.com
export SECRETS_INFISICAL_AUTH_CLIENT_ID=your-machine-identity-client-id
export SECRETS_INFISICAL_AUTH_CLIENT_SECRET=your-machine-identity-client-secret
export SECRETS_INFISICAL_PROJECT_ID=your-project-id
export SECRETS_INFISICAL_ENVIRONMENT=dev
export SECRETS_INFISICAL_SECRET_PATH=/
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithInfisical()
    .FromEnvironment()
    .Build();
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  infisical:
    host:
      uri: https://infisical.example.com
    auth:
      client:
        id: your-machine-identity-client-id
        secret: your-machine-identity-client-secret
    project:
      id: your-project-id
    environment: dev
    secret:
      path: /
```

### Dependency Injection (IOptions&lt;T&gt;)

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureInfisical(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "Infisical": {
      "HostUri": "https://infisical.example.com",
      "ClientId": "your-machine-identity-client-id",
      "ClientSecret": "your-machine-identity-client-secret",
      "ProjectId": "your-project-id",
      "EnvironmentSlug": "dev",
      "SecretPath": "/"
    }
  }
}
```

This reads from the `SecretsManager:Infisical` section and validates all options.

## Authentication

The provider uses [Universal Auth](https://infisical.com/docs/documentation/platform/identities/universal-auth)
with a machine identity. Create the identity in the Infisical organization, attach Universal Auth,
and create a client secret:

1. Create an **organization-scoped** machine identity and grant it a project role.
2. Attach **Universal Auth** to the identity.
3. Create a **client secret** and copy the client ID and client secret.
4. Set `ProjectId`, `EnvironmentSlug` and `SecretPath` to the project, environment and folder that
   contain the secrets.

Organization-scoped identities can be added to a project with a project membership. Project-scoped
identities already belong to one project and cannot be added through the project membership endpoint.

:::warning
Treat client secrets like passwords. Use environment variables or a secret manager to inject them at
runtime, and grant the identity only the project role it needs.
:::

## Local Development

Run a standalone Infisical instance with PostgreSQL and Redis:

```bash title="Terminal"
docker network create infisical

docker run -d --name infisical-db --network infisical \
  -e POSTGRES_USER=infisical -e POSTGRES_PASSWORD=infisical \
  -e POSTGRES_DB=infisical postgres:18

docker run -d --name infisical-redis --network infisical redis:7-alpine

docker run -d --name infisical --network infisical -p 8080:8080 \
  -e ENCRYPTION_KEY=$(openssl rand -hex 16) \
  -e AUTH_SECRET=$(openssl rand -base64 32) \
  -e DB_CONNECTION_URI=postgres://infisical:infisical@infisical-db:5432/infisical \
  -e REDIS_URL=redis://infisical-redis:6379 \
  -e SITE_URL=http://localhost:8080 \
  infisical/infisical:v0.167.4
```

Wait for `http://localhost:8080/api/status`, then bootstrap the instance:

```bash title="Terminal"
curl -X POST http://localhost:8080/api/v1/admin/bootstrap \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@example.com","password":"your-password","organization":"integration"}'
```

The response contains the admin machine identity token and the organization ID. Use that token to
create the project, the organization-scoped identity, the Universal Auth client, and the project
membership.

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

### Concept Mapping

| `ISecretProvider` | Infisical |
|-------------------|-----------|
| Secret key | Secret name |
| Secret value | Secret value |
| Version | Numeric secret version |
| `SecretMetadata.Tags` | Per-secret metadata (`secretMetadata`) |
| `SecretMetadata.ContentType` | Not supported |
| `SecretValue.CreatedAt` / `UpdatedAt` | Not exposed by the .NET SDK |

## Behavior Notes

- **Version selection:** `SecretQuery.Version` passes the numeric version to the Infisical API.
  Invalid version strings are ignored and the current version is returned.
- **Version listing:** Infisical has no version-list endpoint. `GetSecretVersionsAsync` reads the
  current version and probes versions `1` through the current version, so it costs `N + 1` API
  calls. Missing versions are skipped. `CreatedAt` is `null` because the SDK does not expose secret
  timestamps.
- **Metadata:** `SecretMetadata.Tags` is stored as Infisical per-secret metadata. Infisical project
  tags are not supported because they require pre-created tag IDs.
- **Multi-line values:** New and updated values are written with `SkipMultilineEncoding = true` so
  values round-trip unchanged. Values written by other clients with encoding enabled may be returned
  encoded.
- **Delete:** `DeleteSecretAsync` permanently deletes the secret and throws
  `SecretNotFoundException` when the secret does not exist.
- **Missing secrets:** The SDK retries failed GET requests, so `GetSecretAsync` for a missing secret
  can take several seconds before `SecretNotFoundException` is thrown. `SecretExistsAsync` uses a
  list call and does not have this delay.
- **One environment and path:** Each provider instance targets one project, environment and secret
  path. Create multiple providers to read from multiple environments or paths.
- **TLS:** The SDK does not expose an HTTP handler, so there is no `SkipTlsVerify` option. Use a
  certificate that the runtime trusts.
- **Cancellation:** The SDK does not accept a `CancellationToken` for its HTTP calls. The provider
  stops waiting when the token is cancelled, but the SDK request may continue in the background.
- **Authentication methods:** Only Universal Auth is supported. Token Auth, LDAP, cloud IAM and
  Kubernetes authentication are follow-ups.
