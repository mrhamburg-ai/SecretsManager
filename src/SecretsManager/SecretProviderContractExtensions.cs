namespace SecretsManager;

/// <summary>
/// Helpers for implementing the <see cref="ISecretProvider"/> contract.
/// </summary>
public static class SecretProviderContractExtensions
{
    /// <summary>
    /// Throws <see cref="SecretNotFoundException"/> when the given secret does not exist.
    /// </summary>
    /// <param name="provider">The provider to check.</param>
    /// <param name="key">The secret key to look up.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the secret is known to exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the existence check fails.</exception>
    public static async Task EnsureSecretExistsAsync(
        this ISecretProvider provider,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!await provider.SecretExistsAsync(key, cancellationToken).ConfigureAwait(false))
            throw new SecretNotFoundException(key);
    }
}
