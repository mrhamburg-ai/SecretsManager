---
id: doppler
title: Doppler Provider
sidebar_label: Doppler
sidebar_position: 18
---

# Doppler Provider

<span className="provider-badge provider-doppler">Doppler</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

[![Doppler integration tests](https://img.shields.io/github/check-runs/mrhamburg/SecretsManager/main?nameFilter=Integration%20tests%20%28Doppler%29&label=doppler)](https://github.com/mrhamburg/SecretsManager/actions/workflows/ci.yml)

The Doppler integration test runs against a live Doppler project and config when the
`HAS_DOPPLER_SECRETS` repository variable is enabled. A grey `no check runs` pill means the job
has not run on `main` yet.

## Overview

The Doppler provider integrates with [Doppler](https://doppler.com/), a SaaS secrets management
platform. It calls the Doppler REST API v3 directly, because Doppler has no official C# SDK.

**Key benefits:**

- Service tokens give read-only, config-scoped access
- Config-scoped secrets with raw or computed values
- Works with the same `ISecretProvider` API as every other provider
- No extra NuGet dependency beyond the provider package

## Installation

```bash title="Terminal"
dotnet add package mrhamburg.SecretsManager
dotnet add package mrhamburg.SecretsManager.Doppler
```

## Configuration

### Options Reference

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `ApiUrl` | `string` | No | `"https://api.doppler.com"` | Base URL of the Doppler API. Set this only for a proxy or a test double |
| `Token` | `string` | Yes | `""` | Doppler API token. Use a service token, personal token, or service account token |
| `Project` | `string` | Conditional | `""` | Doppler project name. Required unless `Token` is a service token (`dp.st.*`) |
| `Config` | `string` | Conditional | `""` | Doppler config name, for example `dev` or `prd`. Required unless `Token` is a service token |
| `UseRawValues` | `bool` | No | `false` | Return the stored raw value instead of the computed value. Computed values resolve `${SECRET_NAME}` references |

`DopplerOptions.Validate()` requires `ApiUrl` and `Token`. It also requires `Project` and `Config`
when `Token` does not start with `dp.st.`. A service token is bound to one project and config, so
both are optional for it.

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithDoppler(options =>
    {
        options.Token = Environment.GetEnvironmentVariable("SECRETS_DOPPLER_TOKEN")!;
        options.Project = "my-project";
        options.Config = "dev";
        options.UseRawValues = false;
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=doppler
export SECRETS_DOPPLER_TOKEN=dp.st.dev.xxxx
export SECRETS_DOPPLER_PROJECT=my-project
export SECRETS_DOPPLER_CONFIG=dev
export SECRETS_DOPPLER_USE_RAW_VALUES=false
```

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithDoppler()
    .FromEnvironment()
    .Build();
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  doppler:
    api:
      url: https://api.doppler.com
    token: dp.st.dev.xxxx
    project: my-project
    config: dev
    use:
      raw:
        values: false
```

### Dependency Injection (IOptions&lt;T&gt;)

```csharp
var provider = new SecretProviderBuilder()
    .ConfigureDoppler(configuration)
    .Build();
```

```json
{
  "SecretsManager": {
    "Doppler": {
      "ApiUrl": "https://api.doppler.com",
      "Token": "dp.st.dev.xxxx",
      "Project": "my-project",
      "Config": "dev",
      "UseRawValues": false
    }
  }
}
```

This reads from the `SecretsManager:Doppler` section and validates all options.

## Authentication

Doppler uses bearer tokens. Create the token in the Doppler dashboard:

1. Open the project and config that contain the secrets.
2. Open **Access** and create a **Service Token**.
3. Choose read-only access for applications that only read secrets. Choose read/write access when
   the application must create, update, or delete secrets.
4. Copy the token and store it as a secret, for example in `SECRETS_DOPPLER_TOKEN`.

Service tokens (`dp.st.*`) are scoped to one project and config. Personal tokens (`dp.pt.*`) and
service account tokens (`dp.sa.*`) have wider access and need `Project` and `Config`.

:::warning
Treat Doppler tokens like passwords. Use environment variables or a secret manager to inject them
at runtime. Give each application the narrowest token scope it needs.
:::

## Usage

```csharp
var secret = await provider.GetSecretAsync("DATABASE_URL");
await provider.PutSecretAsync("NEW_SECRET", "secret-value");
var versions = await provider.GetSecretVersionsAsync("DATABASE_URL");
await provider.DeleteSecretAsync("OLD_SECRET");
```

### Concept Mapping

| `ISecretProvider` | Doppler |
|-------------------|---------|
| Secret key | Secret name |
| Secret value | Computed value by default; raw value when `UseRawValues` is `true` |
| Version | Not available. `SecretValue.Version` is `null` |
| `SecretQuery.Version` | Not supported. A non-null value throws `SecretProviderException` |
| `SecretMetadata.Tags` | Not supported. Ignored |
| `SecretMetadata.ContentType` | Not supported. Ignored |
| `SecretValue.CreatedAt` / `UpdatedAt` | Not exposed by the API |

## Behavior Notes

- **Computed values:** Doppler resolves references such as `${USER}` when a secret is read. The
  provider returns the computed value by default. Set `UseRawValues` to `true` to read the stored
  text, which can still contain `${...}` references.
- **Version history:** Doppler keeps version history in its config logs, but the public secrets API
  does not expose per-secret versions. `GetSecretVersionsAsync` returns one current entry, and
  `SecretQuery.Version` is not supported.
- **Metadata:** Doppler has secret notes and project tags, but neither maps to `SecretMetadata`.
  The provider ignores `Tags` and `ContentType`.
- **Delete:** `DeleteSecretAsync` permanently deletes the secret and throws
  `SecretNotFoundException` when the secret does not exist.
- **Existence checks:** `SecretExistsAsync` uses `GET /v3/configs/config/secrets/names`. The
  single-secret endpoint can return a deleted secret for a short time, so it is not used for this
  check. A `404` from the names endpoint means the project or config is missing, so the provider
  throws `SecretProviderException` instead of reporting a missing secret.
- **Restricted values:** A secret with restricted visibility has no readable value. The provider
  throws `SecretProviderException` with a clear message.
- **Errors:** `SecretProviderException` includes Doppler's `messages` text when the API returns it.
  The provider redacts the token and the submitted value from that text.
- **Empty values:** The provider sends the value as-is. If Doppler rejects an empty value, the API
  error is reported as `SecretProviderException`.
- **Secret names:** Doppler has Classic and Permissive naming modes. The provider accepts any
  non-empty name and sends it as an encoded query parameter.
- **One config per provider:** Each provider instance targets one project and config. Create
  multiple providers to read from multiple configs.
- **Rate limits:** Doppler returns `429` with a `retry-after` header when a limit is reached. The
  provider reports the status and wait time. Use the retry layer for production workloads.
- **Cancellation:** The provider passes the `CancellationToken` to every HTTP request.
- **Self-hosting:** Doppler is a hosted SaaS product. `ApiUrl` is configurable for a proxy or a
  test double, not for a self-hosted Doppler server.

## Integration Test

The shared lifecycle harness runs against a dedicated Doppler project and config. Set these
repository secrets and variable:

- `DOPPLER_TOKEN`: a read/write service token for the test project and config
- `DOPPLER_PROJECT`: the test project name
- `DOPPLER_CONFIG`: the test config name
- `HAS_DOPPLER_SECRETS`: set to `true` to enable the job

The job runs on `main` pushes and manual runs, not on pull requests. Doppler reports one version,
so the job passes `expect-version-history: 'false'`. It also passes
`key-prefix: 'INTEGRATION_TEST_'` so the generated key is valid under Doppler's Classic naming
mode. Never point the test token at a production config.
