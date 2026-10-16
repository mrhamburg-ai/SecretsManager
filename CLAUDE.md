# CLAUDE.md

Guidance for AI coding agents working in this repository.

## Project Overview

SecretsManager is a .NET 10 library for unified secret management across 14 cloud providers. It uses a fluent builder pattern with optional `IOptions<T>` DI support.

**Architecture:** Core abstraction (`ISecretProvider`) + 14 provider packages. Each provider is an independent NuGet package with its own options class, factory, and configuration bridge.

## Build, Test, Format

All commands run inside the `dev-env` Docker container:

```bash
# Build
docker exec -w /workspace/projects/SecretsManager dev-env dotnet build SecretsManager.sln

# Test
docker exec -w /workspace/projects/SecretsManager dev-env dotnet test SecretsManager.sln --no-build --verbosity normal

# Format check
docker exec -w /workspace/projects/SecretsManager dev-env dotnet format SecretsManager.sln --verify-no-changes --no-restore

# Pack
docker exec -w /workspace/projects/SecretsManager dev-env dotnet pack SecretsManager.sln --no-build -c Release --output ./nupkg
```

**Expected:** 0 errors, 0 format violations, 262+ tests pass.

## PR Conventions

1. **Always branch from latest origin/main:** `git fetch origin main && git checkout -b <branch> origin/main`
2. **Commit format:** `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `ci:`, `test:` (conventional commits)
3. **PR titles:** Same prefix as commits
4. **Labels:** `feature`, `enhancement`, `bug`, `documentation`, `ci`, `refactor`, `security`, `monitoring`, `resilience`, `packaging`, `release`, `breaking-change`
5. **Reviewers:** Add `mrhamburg` as reviewer in GitHub UI (fork owners can't request via API)
6. **Build before push:** Always run the full CI simulation locally before pushing

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

All package versions are in `Directory.Packages.props`. Never specify `Version` on `PackageReference` in `.csproj` files — it will cause NU1008 errors.

## Documentation Conventions

- **XML docs required** on all public properties in options classes
- **MDX escaping:** In markdown docs, escape `IOptions<T>` as `IOptions&lt;T&gt;` — MDX parses `<T>` as a JSX tag
- **Provider docs:** Each provider has a doc page at `docs/docs/providers/<name>.md` with a "Configuration via IOptions<T>" subsection
- **Configuration guide:** `docs/docs/getting-started/configuration.md` lists all providers — keep in sync with actual providers

## CI/CD

- **ci.yml:** Restore → API compat check → format → build → test → pack
- **docs-build.yml:** `npm ci && npm run build` in `docs/` (Docusaurus)
- **integration.yml:** Runs integration tests against real services (PostgreSQL, Vault, Conjur)
- **release.yml:** Packs and publishes NuGet packages

## Gotchas

- **MDX + IOptions<T>:** `<T>` in markdown headings breaks Docusaurus builds. Always escape as `&lt;T&gt;`.
- **CPM:** `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` — no `Version` on `PackageReference`.
- **Architecture tests:** Core project must NOT reference provider projects. Use `InternalsVisibleTo` for test access.
- **GitHub token:** `GITHUB_CLASSIC_TOKEN` in `~/.hermes/.env` (user `mrhamburg-ai`). Can push/PR but cannot create labels or request reviewers.
- **Fork workflow:** Push to `mrhamburg-ai/SecretsManager` fork, create PR to `mrhamburg/SecretsManager:main`.
- **API compatibility checker:** `tools/ApiCompatChecker/` — reflection-based tool comparing two build directories. CI compares main vs PR branch.
