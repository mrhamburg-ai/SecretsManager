using SecretsManager;
using SecretsManager.Builder;
using CoreSecretMetadata = SecretsManager.SecretMetadata;
using SecretsManager.FileSystem;
using SecretsManager.AzureKeyVault;
using SecretsManager.Scaleway;
using SecretsManager.PostgreSql;
using SecretsManager.OVH;
using SecretsManager.AwsSecretsManager;
using SecretsManager.OracleVault;
using SecretsManager.GoogleSecretManager;
using SecretsManager.Passbolt;
using SecretsManager.AliyunKms;
using SecretsManager.IBMCloudSecretsManager;
using SecretsManager.Infisical;
using SecretsManager.TencentCloud;
using SecretsManager.Vault;
using SecretsManager.Conjur;

if (args.Length == 0)
{
    Console.WriteLine("Usage: SecretsManager.IntegrationTests <config.yaml>");
    Console.WriteLine();
    Console.WriteLine("Runs a full secret lifecycle test (put, get, exists, versions, delete)");
    Console.WriteLine("against the provider configured in the YAML file.");
    Console.WriteLine();
    Console.WriteLine("Sample configs are in the configs/ directory.");
    return 1;
}

var configPath = args[0];
if (!File.Exists(configPath))
{
    WriteError($"Config file not found: {configPath}");
    return 1;
}

Console.WriteLine($"Loading config from: {configPath}");
Console.WriteLine();

ISecretProvider provider;
try
{
    provider = new SecretProviderBuilder()
        .WithFileSystem()
        .WithAzureKeyVault()
        .WithScaleway()
        .WithPostgreSql()
        .WithOVH()
        .WithAwsSecretsManager()
        .WithOracleVault()
        .WithGoogleSecretManager()
        .WithPassbolt()
        .WithAliyunKms()
        .WithTencentCloud()
        .WithIBMCloudSecretsManager()
        .WithInfisical()
        .WithVault()
        .WithConjur()
        .FromYamlFile(configPath)
        .Build();
}
catch (Exception ex)
{
    WriteError($"Failed to build provider: {ex.Message}");
    return 1;
}

await using (provider)
{
    var testKey = $"integration-test-{Guid.NewGuid().ToString("N")[..8]}";
    var passed = 0;
    var failed = 0;

    // Step 1: Put a secret
    await RunStep("Put secret (v1)", async () =>
    {
        var result = await provider.PutSecretAsync(testKey, "test-value-1");
        Assert(result.Key == testKey, $"Expected key '{testKey}', got '{result.Key}'");
        Assert(result.Value == "test-value-1", $"Expected value 'test-value-1', got '{result.Value}'");
        Console.WriteLine($"  Created version: {result.Version ?? "(none)"}");
    });

    // Step 2: Get the secret
    await RunStep("Get secret", async () =>
    {
        var result = await provider.GetSecretAsync(testKey);
        Assert(result.Value == "test-value-1", $"Expected 'test-value-1', got '{result.Value}'");
    });

    // Step 3: Update the secret
    await RunStep("Put secret (v2)", async () =>
    {
        var result = await provider.PutSecretAsync(testKey, "test-value-2");
        Assert(result.Value == "test-value-2", $"Expected 'test-value-2', got '{result.Value}'");
        Console.WriteLine($"  Created version: {result.Version ?? "(none)"}");
    });

    // Step 4: Get updated value
    await RunStep("Get updated secret", async () =>
    {
        var result = await provider.GetSecretAsync(testKey);
        Assert(result.Value == "test-value-2", $"Expected 'test-value-2', got '{result.Value}'");
    });

    // Step 5: Check existence
    await RunStep("Secret exists", async () =>
    {
        var exists = await provider.SecretExistsAsync(testKey);
        Assert(exists, "Expected secret to exist");
    });

    // Step 6: List versions
    await RunStep("List versions", async () =>
    {
        var versions = await provider.GetSecretVersionsAsync(testKey);
        Assert(versions.Count >= 2, $"Expected at least 2 versions, got {versions.Count}");
        Console.WriteLine($"  Found {versions.Count} version(s):");
        foreach (var v in versions)
            Console.WriteLine($"    - {v.Version} (current: {v.IsCurrent}, created: {v.CreatedAt})");
    });

    // Step 7: Put with metadata
    await RunStep("Put secret with metadata", async () =>
    {
        var metadata = new CoreSecretMetadata
        {
            ContentType = "text/plain",
            Tags = new Dictionary<string, string> { ["env"] = "integration-test" }
        };
        var result = await provider.PutSecretAsync(testKey, "test-value-3", metadata);
        Assert(result.Value == "test-value-3", $"Expected 'test-value-3', got '{result.Value}'");
    });

    // Step 8: Delete the secret
    await RunStep("Delete secret", async () =>
    {
        await provider.DeleteSecretAsync(testKey);
    });

    // Step 9: Verify deleted
    await RunStep("Verify deleted", async () =>
    {
        var exists = await provider.SecretExistsAsync(testKey);
        Assert(!exists, "Expected secret to not exist after deletion");
    });

    // Summary
    Console.WriteLine();
    Console.WriteLine(new string('-', 50));
    if (failed == 0)
    {
        WriteSuccess($"All {passed} steps passed!");
        return 0;
    }
    else
    {
        WriteError($"{failed} of {passed + failed} steps failed.");
        return 1;
    }

    async Task RunStep(string name, Func<Task> action)
    {
        Console.Write($"  [{passed + failed + 1}] {name}... ");
        try
        {
            await action();
            WriteSuccess("PASS");
            passed++;
        }
        catch (Exception ex)
        {
            WriteError($"FAIL - {ex.Message}");
            failed++;
        }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void WriteSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(message);
    Console.ResetColor();
}

static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ResetColor();
}
