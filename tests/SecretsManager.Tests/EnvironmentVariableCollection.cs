namespace SecretsManager.Tests;

/// <summary>
/// Serialises tests that read or write process-wide environment variables.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableCollection
{
    /// <summary>
    /// The collection name shared by environment-sensitive test classes.
    /// </summary>
    public const string Name = "Environment variables";
}
