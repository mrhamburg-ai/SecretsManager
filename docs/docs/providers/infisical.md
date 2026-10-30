---
id: infisical
title: Infisical Provider
sidebar_label: Infisical
sidebar_position: 17
---

# Infisical Provider

<span className="provider-badge provider-infisical">Infisical</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

[![Infisical integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Infisical%29&label=infisical)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

The Infisical integration test runs against a live self-hosted Infisical instance on
`main`. A grey `no check runs` pill means the job has not run on `main` yet.

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
| `ExpandSecretReferences` | `bool` | No | `true` | Whether Infisical expands `${OTHER_SECRET}` references when a secret is read. Set to `false` to read back exactly the stored value |

`InfisicalOptions.Validate()` requires `HostUri`, `ClientId`, `ClientSecret`, `ProjectId`,
`EnvironmentSlug` and `SecretPath`. The default values satisfy the `HostUri`, `EnvironmentSlug` and
`SecretPath` requirements. `ExpandSecretReferences` is a flag and needs no validation.

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
        options.ExpandSecretReferences = true;
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
export SECRETS_INFISICAL_EXPAND_SECRET_REFERENCES=true
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
    expand:
      secret:
        references: true
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
      "SecretPath": "/",
      "ExpandSecretReferences": true
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
| `SecretMetadata.Tags` | Per-secret metadata (`secretMetadata`). `null` leaves existing metadata unchanged and an empty dictionary clears it |
| `SecretMetadata.ContentType` | Not supported |
| `SecretValue.CreatedAt` / `UpdatedAt` | Not exposed by the .NET SDK |

## Behavior Notes

- **Version selection:** `SecretQuery.Version` must be a positive integer. A non-numeric, zero, or
  negative version throws `SecretProviderException`.
- **Version listing:** The .NET SDK has no version-list endpoint, and the enterprise version-history
  endpoint is not reachable with Universal Auth. `GetSecretVersionsAsync` reads the current version
  and probes versions `1` through the current version, so it costs `N + 1` API calls. Missing
  versions are skipped. Histories longer than 1000 versions are rejected to bound the request count.
  `CreatedAt` is `null` because the SDK does not expose secret timestamps.
- **Metadata:** `SecretMetadata.Tags` is stored as Infisical per-secret metadata. Infisical project
  tags are not supported because they require pre-created tag IDs. On update, `null` tags leave the
  existing metadata unchanged and an empty dictionary removes it. Infisical's create and update
  responses do not include metadata, so `PutSecretAsync` reports the tags that are now stored
  (the tags you sent, or the existing ones when you sent none).
- **Secret keys:** The SDK appends the key to the request path without escaping it. To avoid
  addressing the wrong secret or API route, keys that are empty, equal to `.` or `..`, or contain
  `/`, `\`, `?` or `#` are rejected with `SecretProviderException` before any request is sent.
- **Empty values:** Infisical rejects empty secret values, so `PutSecretAsync` with an empty string
  throws `SecretProviderException`.
- **Secret references:** By default Infisical expands references such as `${OTHER_SECRET}` when a
  secret is read, so a value written as `pre-${OTHER_SECRET}-post` is returned expanded by
  `GetSecretAsync`, while `PutSecretAsync` returns it as stored. Set `ExpandSecretReferences` to
  `false` to read back exactly what was written.
- **Multi-line values:** New and updated values are written with `SkipMultilineEncoding = true` so
  values round-trip unchanged. Values written by other clients with encoding enabled may be returned
  encoded.
- **Stored values:** `PutSecretAsync` returns the value stored by Infisical. Infisical may trim
  leading or trailing whitespace.
- **Approval policies:** Projects with secret approval policies may return an approval request
  instead of a secret. The provider reports this as an empty response; approval workflows are not
  supported.
- **Imported secrets:** The SDK includes imported secrets in list results. An imported key can be
  read but cannot be updated through this provider.
- **Delete:** `DeleteSecretAsync` permanently deletes the secret and throws
  `SecretNotFoundException` when the secret does not exist.
- **Missing secrets:** The SDK retries every failed GET request three times with 2, 4 and 8 second
  waits, so `GetSecretAsync` for a missing secret (or a missing version) takes about 14 seconds
  before `SecretNotFoundException` is thrown. `SecretExistsAsync` uses a list call and does not have
  this delay, so check it first when a secret may not exist.
- **One environment and path:** Each provider instance targets one project, environment and secret
  path. Create multiple providers to read from multiple environments or paths.
- **TLS:** The SDK does not expose an HTTP handler, so there is no `SkipTlsVerify` option. Use a
  certificate that the runtime trusts.
- **Cancellation:** The SDK does not accept a `CancellationToken` for its HTTP calls. The provider
  stops waiting when the token is cancelled, but the SDK request may continue in the background.
- **Authentication methods:** Only Universal Auth is supported. Token Auth, LDAP, cloud IAM and
  Kubernetes authentication are follow-ups.
