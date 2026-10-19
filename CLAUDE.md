# CLAUDE.md

Guidance for AI coding agents working in this repository.

## Project Overview

SecretsManager is a .NET 10 library for unified secret management across 14 cloud providers. It uses a fluent builder pattern with optional `IOptions<T>` DI support.

**Architecture:** Core abstraction (`ISecretProvider`) + 14 provider packages. Each provider is an independent NuGet package with its own options class, factory, and configuration bridge.

The core also ships cross-cutting decorators (`ISecretProviderLayer`): retry, timeout, concurrency limiting, logging, and OpenTelemetry tracing/metrics. The `FileSystem` and `PostgreSQL` providers share AES-256-GCM encryption (`AesGcmEncryptor`), and a key can be sourced from another provider via `SecretProviderKeySource`.

## Repository Layout

- `src/SecretsManager/` — core abstraction, fluent builder, layers, encryption.
- `src/SecretsManager.<Provider>/` — one NuGet package per provider (14 total).
- `tests/SecretsManager.<Provider>.Tests/` — xUnit + NSubstitute unit tests.
- `tests/SecretsManager.ArchitectureTests/` — architecture guards.
- `tests/SecretsManager.IntegrationTests/` — manual console harness, not an xUnit project.
- `tools/ApiCompatChecker/` — reflection-based API diff tool.
- `docs/` — Docusaurus site; provider pages live in `docs/docs/providers/`.

## Build, Test, Format

Requires the **.NET 10 SDK**.

```bash
dotnet restore
dotnet build SecretsManager.sln -c Release
dotnet test SecretsManager.sln -c Release
dotnet format SecretsManager.sln --verify-no-changes --no-restore
dotnet pack SecretsManager.sln --no-build -c Release --output ./nupkg
```

- `dotnet test SecretsManager.sln -c Release` builds and runs the unit test projects registered in the solution. Use it instead of `--no-build`, which only works after a prior build.
- The `IntegrationTests` project is a console harness, not a test project, so `dotnet test` does not execute it.
- **Expected:** 0 errors, 0 format violations, all tests pass.

## Compiler Settings That Affect Agents

From `Directory.Build.props` (applies to all projects):

- `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`.
- `GenerateDocumentationFile=true` **and** `TreatWarningsAsErrors=true`. Every publicly visible type/member needs an XML doc comment — missing docs fail the build (CS1591), and any warning fails the build.
- Test projects set `GenerateDocumentationFile=false` via `tests/Directory.Build.props`, so test fixtures do not need docs.

## PR Conventions

1. **Always branch from latest origin/main:** `git fetch origin main && git checkout -b <branch> origin/main`
2. **Commit format:** `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `ci:`, `test:` (conventional commits)
3. **PR titles:** Same prefix as commits
4. **Labels:** `feature`, `enhancement`, `bug`, `documentation`, `ci`, `refactor`, `security`, `monitoring`, `resilience`, `packaging`, `release`, `breaking-change`
5. **Build before push:** Always run the full CI simulation locally before pushing

## Code Patterns

### Options Class

Every provider has an `XxxOptions` class with:
- Public properties with XML `<summary>` docs
- A `Validate()` method returning `IEnumerable<string>` (empty = valid)

```csharp
public sealed class VaultOptions
{
    /// <summary>
    /// Vault server URL (e.g. "http://localhost:8200").
    /// </summary>
    public string Url { get; set; } = "http://localhost:8200";

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            yield return "Url is required";
    }
}
```

### Configuration Bridge

Each provider has a `ConfigureXxx(IConfiguration)` extension that binds config → options → builder with validation:

```csharp
public static SecretProviderBuilder ConfigureVault(
    this SecretProviderBuilder builder,
    IConfiguration configuration)
{
    var options = configuration.GetSection("SecretsManager:Vault").Get<VaultOptions>()
        ?? new VaultOptions();
    var errors = options.Validate().ToList();
    if (errors.Count > 0)
        throw new ArgumentException($"Configuration errors: {string.Join("; ", errors)}");
    return builder.WithVault(o => { o.Url = options.Url; });
}
```

### DI Registration

```csharp
builder.Services.AddSecretsManager();
builder.Services.Configure<VaultOptions>(configuration.GetSection("SecretsManager:Vault"));
```

### Central Package Management

All package versions are in `Directory.Packages.props`. Never specify `Version` on `PackageReference` in `.csproj` files — it will cause NU1008 errors. To add a package, add a `<PackageVersion>` entry there and reference it by name.

### Builder Configuration Sources

The fluent builder can be fed from three interchangeable sources, all producing flat `Dictionary<string, string>` settings:

- `FromEnvironment(prefix = "SECRETS")` / `EnvironmentVariableConfigurationReader`
- `FromYamlFile(path)` / `FromYaml(yaml)` / `YamlConfigurationReader`
- `ConfigureXxx(IConfiguration)` bridges

## Testing Conventions

- **Unit tests:** xUnit + NSubstitute, one project per provider, namespace `SecretsManager.<Provider>.Tests`.
- **Architecture tests:** `tests/SecretsManager.ArchitectureTests/` asserts the core does not reference any provider and that providers reference the core. `ProviderAssemblies` currently lists only the Vault and AWS factories — extend it when it matters for a change.
- **Integration tests:** `tests/SecretsManager.IntegrationTests` is an executable console app driven by a YAML config (`dotnet run -- <config.yaml>`). Sample configs live in `tests/SecretsManager.IntegrationTests/configs/`. It is not executed by `dotnet test`.
- **New projects must be added to `SecretsManager.sln`** (see README "Adding a New Provider"), otherwise `dotnet build`/`dotnet test` will skip them.

## Documentation Conventions

- **XML docs required** on all public properties in options classes (enforced by the build).
- **MDX escaping:** In markdown docs, escape `IOptions<T>` as `IOptions&lt;T&gt;` — MDX parses `<T>` as a JSX tag.
- **Provider docs:** Each provider has a doc page at `docs/docs/providers/<name>.md`. Its `## Configuration` section must contain, in order: an `### Options Reference` table listing every setting from that provider's options class with type, required status (`Yes` / `Conditional` / `No`), default and description; then the `### Fluent API`, `### Environment Variables` and `### YAML Configuration` examples; then a `### Dependency Injection (IOptions<T>)` subsection. Required-ness comes from the options class's `Validate()` method — never from prose.
- **Configuration guide:** `docs/docs/getting-started/configuration.md` lists all providers — keep in sync with actual providers. The `docs/docs/providers/overview.md` provider grid and feature-comparison table must also be kept in sync.
- **Advanced guides:** `docs/docs/advanced/` covers layering, versioning, JSON extraction, and custom providers.

## CI/CD

- **ci.yml:** restore → format (repo-wide) → build (Release) → unit tests (filtered) → pack. The API compatibility check step is currently commented out.
- **docs-build.yml:** `npm ci && npm run build` in `docs/`, then deploys to GitHub Pages.
- **integration.yml:** runs the integration harness against real services (PostgreSQL, Vault, Conjur, and cloud providers gated by repository variables). Triggers on main pushes, PRs to main, and manual dispatch.
- **release.yml:** packs each `src/*/*.csproj` with `/p:Version` and publishes to NuGet on release publish or manual dispatch.

## Gotchas

- **TreatWarningsAsErrors + CS1591:** any missing XML doc on a public member breaks the build. Fix docs, don't suppress.
- **MDX + IOptions<T>:** `<T>` in markdown headings breaks Docusaurus builds. Always escape as `&lt;T&gt;`.
- **CPM:** `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` — no `Version` on `PackageReference`.
- **Architecture tests:** Core project must NOT reference provider projects. Use `InternalsVisibleTo` for test access.
- **API compatibility checker:** `tools/ApiCompatChecker/` — reflection-based tool comparing two build directories. Its CI step is currently disabled.
- **Integration tests are not xUnit:** don't expect them under `dotnet test`; run them via `dotnet run -- <config.yaml>`.

## Common Tasks

### Add a new provider

1. Create `src/SecretsManager.<Name>/` referencing the core `SecretsManager` project.
2. Implement `ISecretProvider` and an `ISecretProviderFactory` that maps flat config dictionaries to your options.
3. Add a `SecretProviderBuilderExtensions` class exposing `.With<Name>()` (and `.Configure<Name>(IConfiguration)`).
4. Add `tests/SecretsManager.<Name>.Tests/`.
5. Register both projects in `SecretsManager.sln`.
6. Add `docs/docs/providers/<name>.md` and link it from `docs/docs/getting-started/configuration.md` and `docs/docs/providers/overview.md`.

### Add a layer

Implement `ISecretProviderLayer` (see `src/SecretsManager/Layers/`), add a matching `<X>LayerOptions`, and expose a `With<X>()` extension on `SecretProviderBuilderLayerExtensions`.

## Local Preferences

For environment-specific settings (container paths, tokens, fork workflow), see `CLAUDE_LOCAL.md` (gitignored, optional).
