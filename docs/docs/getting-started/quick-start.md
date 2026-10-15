---
id: quick-start
title: Quick Start
sidebar_label: Quick Start
sidebar_position: 1
---

# Quick Start

Get up and running with SecretsManager in under 5 minutes.

## Prerequisites

- .NET 10.0 SDK or later
- A C# project (Console, Web API, or any .NET application)
- Access to at least one secret backend (local filesystem works for testing)

## Step 1: Install Packages

First, install the core library and your chosen provider. For this example, we'll use the FileSystem provider:

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.FileSystem
```

## Step 2: Configure Provider

Configure the provider using the Fluent API. Add this to your `Program.cs` or startup code:

```csharp title="Program.cs"
using SecretsManager;
using SecretsManager.Builder;
using SecretsManager.FileSystem;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register SecretsManager
builder.Services.AddSingleton<ISecretProvider>(sp =>
{
    return new SecretProviderBuilder()
        .WithFileSystem(options =>
        {
            options.BasePath = "/var/secrets";
            options.EncryptionKey = builder.Configuration["Encryption:Key"];
        })
        .Build();
});

var app = builder.Build();
app.Run();
```

You can also add resilient and observable behavior in one fluent chain:

```csharp title="Program.cs"
using SecretsManager;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ISecretProvider>(sp =>
{
    return new SecretProviderBuilder()
        .WithFileSystem(options =>
        {
            options.BasePath = "/var/secrets";
            options.EncryptionKey = builder.Configuration["Encryption:Key"];
        })
        .WithRetry()
        .WithTimeout(options => options.Timeout = TimeSpan.FromSeconds(5))
        .WithLogging()
        .Build();
});
```

:::info[Configuration Options]
You can also configure providers using environment variables or YAML files. See the [Configuration guide](/docs/getting-started/configuration) for details.
:::

## Step 3: Use the API

Inject `ISecretProvider` into your services and start managing secrets:

```csharp title="Controllers/SecretsController.cs"
using Microsoft.AspNetCore.Mvc;
using SecretsManager;

[ApiController]
[Route("api/[controller]")]
public class SecretsController : ControllerBase
{
    private readonly ISecretProvider _secretProvider;

    public SecretsController(ISecretProvider secretProvider)
    {
        _secretProvider = secretProvider;
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetSecret(string name)
    {
        try
        {
            var secret = await _secretProvider.GetSecretAsync(name);
            return Ok(new { value = secret.Value });
        }
        catch (SecretNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{name}")]
    public async Task<IActionResult> PutSecret(string name, [FromBody] string value)
    {
        await _secretProvider.PutSecretAsync(name, value);
        return Ok();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteSecret(string name)
    {
        await _secretProvider.DeleteSecretAsync(name);
        return NoContent();
    }
}
```

:::tip[Best Practices]
- Never hardcode encryption keys or credentials in your source code
- Use environment variables or Azure Key Vault references for sensitive configuration
- Enable versioning for critical secrets to maintain audit trails
- Implement proper error handling for `SecretNotFoundException`
:::

## Next Steps

- [Explore other providers](/docs/providers/overview)
- [Learn about secret versioning](/docs/advanced/versioning)
- [Extract values from JSON secrets](/docs/advanced/json-extraction)
- [Add cross-cutting behavior with layers](/docs/advanced/layering)
