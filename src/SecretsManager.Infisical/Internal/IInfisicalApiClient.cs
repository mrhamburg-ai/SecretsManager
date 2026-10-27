namespace SecretsManager.Infisical.Internal;

/// <summary>
/// A secret returned by the Infisical API.
/// </summary>
/// <param name="Key">The secret name.</param>
/// <param name="Value">The secret value. Empty when the value was not requested.</param>
/// <param name="Version">The numeric secret version.</param>
/// <param name="Metadata">Per-secret metadata, or <see langword="null"/> when none is present.</param>
internal sealed record InfisicalSecretResult(
    string Key,
    string Value,
    int Version,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>
/// Abstraction over the Infisical SDK so the provider can be tested without network calls.
/// </summary>
internal interface IInfisicalApiClient : IAsyncDisposable
{
    /// <summary>
    /// Finds a secret in the configured environment and path without retrieving its value.
    /// </summary>
    Task<InfisicalSecretResult?> FindSecretAsync(
        string key, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a secret, optionally by version, with or without its value.
    /// </summary>
    Task<InfisicalSecretResult?> GetSecretAsync(
        string key, int? version, bool includeValue, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a secret.
    /// </summary>
    Task<InfisicalSecretResult> CreateSecretAsync(
        string key,
        string value,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing secret.
    /// </summary>
    Task<InfisicalSecretResult> UpdateSecretAsync(
        string key,
        string value,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a secret. Returns <see langword="false"/> when the secret does not exist.
    /// </summary>
    Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken);
}
