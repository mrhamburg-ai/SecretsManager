using SecretsManager.Doppler.Internal;
using SecretsManager.Internal;

namespace SecretsManager.Doppler;

/// <summary>
/// Stores and retrieves secrets in Doppler through the REST API. Supports raw or computed values
/// and JSON property extraction. Doppler does not expose per-secret versions through the API.
/// </summary>
public sealed class DopplerSecretProvider : ISecretProvider
{
    private readonly IDopplerApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="DopplerSecretProvider"/> class.
    /// </summary>
    /// <param name="options">Connection and authentication options for Doppler.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the options are invalid.</exception>
    public DopplerSecretProvider(DopplerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Invalid Doppler options: {string.Join("; ", errors)}", nameof(options));

        _client = new DopplerApiClient(options);
    }

    internal DopplerSecretProvider(IDopplerApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret from Doppler, optionally extracting a JSON property from the value.
    /// </summary>
    /// <param name="key">The Doppler secret name.</param>
    /// <param name="query">Optional JSON property selector. Version selection is not supported.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The secret value. <see cref="SecretValue.Version"/> is always
    /// <see langword="null"/>.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">
    /// Thrown when a version is requested, the value is not readable, or the Doppler API call
    /// fails.
    /// </exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (query?.Version is not null)
                throw new SecretProviderException(
                    "Doppler does not expose per-secret versions. " +
                    "Do not set SecretQuery.Version.");

            var secret = await _client.GetSecretAsync(key, cancellationToken).ConfigureAwait(false)
                ?? throw new SecretNotFoundException(key);

            var value = secret.Value;
            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            return new SecretValue
            {
                Key = key,
                Value = value
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException
            and not OperationCanceledException and not ArgumentException)
        {
            throw new SecretProviderException(
                $"Doppler error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in Doppler and returns the stored value.
    /// </summary>
    /// <param name="key">The Doppler secret name.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="metadata">Optional metadata; Doppler does not support tags or content types,
    /// so this parameter is ignored.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The stored value. <see cref="SecretValue.Version"/> is always
    /// <see langword="null"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Doppler API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await _client.PutSecretAsync(key, value, cancellationToken).ConfigureAwait(false);

            return new SecretValue
            {
                Key = key,
                Value = stored.Value
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException
            and not OperationCanceledException and not ArgumentException)
        {
            throw new SecretProviderException(
                $"Doppler error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from Doppler.
    /// </summary>
    /// <param name="key">The Doppler secret name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Doppler API call fails.</exception>
    public async Task DeleteSecretAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _client.DeleteSecretAsync(key, cancellationToken).ConfigureAwait(false))
                throw new SecretNotFoundException(key);
        }
        catch (Exception ex) when (ex is not SecretProviderException
            and not OperationCanceledException and not ArgumentException)
        {
            throw new SecretProviderException(
                $"Doppler error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns the current version of a secret. Doppler has no per-secret version list, so this
    /// method always returns one entry.
    /// </summary>
    /// <param name="key">The Doppler secret name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One entry that marks the current value.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Doppler API call fails.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _client.SecretExistsAsync(key, cancellationToken).ConfigureAwait(false))
                throw new SecretNotFoundException(key);

            return new[]
            {
                new SecretVersionInfo
                {
                    Version = "1",
                    IsCurrent = true
                }
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException
            and not OperationCanceledException and not ArgumentException)
        {
            throw new SecretProviderException(
                $"Doppler error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Doppler without reading its value.
    /// </summary>
    /// <param name="key">The Doppler secret name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Doppler API call fails.</exception>
    public async Task<bool> SecretExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.SecretExistsAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not SecretProviderException
            and not OperationCanceledException and not ArgumentException)
        {
            throw new SecretProviderException(
                $"Doppler error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Disposes the underlying Doppler API client.
    /// </summary>
    public ValueTask DisposeAsync() =>
        _client.DisposeAsync();
}
