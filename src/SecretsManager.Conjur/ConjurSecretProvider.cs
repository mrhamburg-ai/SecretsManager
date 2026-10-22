using SecretsManager.Conjur.Internal;
using SecretsManager.Internal;

namespace SecretsManager.Conjur;

/// <summary>
/// Stores and retrieves secrets in CyberArk Conjur, authenticating with a login and API key to
/// obtain a short-lived access token. Supports numeric version selection and JSON property extraction.
/// </summary>
public sealed class ConjurSecretProvider : ISecretProvider
{
    private readonly IConjurApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConjurSecretProvider"/> class.
    /// </summary>
    /// <param name="options">Connection and authentication options for the Conjur server.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="ConjurOptions.Url"/>, <see cref="ConjurOptions.Account"/>,
    /// <see cref="ConjurOptions.Login"/> or <see cref="ConjurOptions.ApiKey"/> is empty.
    /// </exception>
    public ConjurSecretProvider(ConjurOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Url))
            throw new ArgumentException("Url is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Account))
            throw new ArgumentException("Account is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Login))
            throw new ArgumentException("Login is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _client = new ConjurApiClient(options);
    }

    internal ConjurSecretProvider(IConjurApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret from Conjur, optionally selecting a numeric version and/or extracting a
    /// JSON property from the stored value.
    /// </summary>
    /// <param name="key">The Conjur secret path.</param>
    /// <param name="query">Optional version and/or JSON property selector.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The secret value, with its resolved version and creation time.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Conjur API call fails.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            int? version = null;
            if (query?.Version is { } v && int.TryParse(v, out var parsed))
                version = parsed;

            var value = await _client.GetSecretValueAsync(key, version, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            if (query?.Property is { } property)
                value = JsonPropertyExtractor.Extract(value, property);

            var resolved = version.HasValue
                ? await FindVersionAsync(key, version.Value, cancellationToken)
                : await LatestVersionAsync(key, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = resolved?.Version.ToString(),
                CreatedAt = resolved?.CreatedAt
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Conjur error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or replaces a secret in Conjur and returns the newly created version.
    /// </summary>
    /// <param name="key">The Conjur secret path.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="metadata">Optional metadata; not persisted by Conjur.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The stored value, with its resolved version and creation time.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Conjur API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PutSecretAsync(key, value, cancellationToken);

            var latest = await LatestVersionAsync(key, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = latest?.Version.ToString(),
                CreatedAt = latest?.CreatedAt
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Conjur error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret by patching the target policy with a <c>!delete</c> statement, since Conjur
    /// exposes no direct delete API.
    /// </summary>
    /// <param name="key">The Conjur secret path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="SecretProviderException">Thrown when the Conjur API call fails.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteSecretAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Conjur error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists all versions of a secret in Conjur.
    /// </summary>
    /// <param name="key">The Conjur secret path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One entry per version, with the current version flagged.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Conjur API call fails.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _client.ListVersionsAsync(key, cancellationToken);

            return versions
                .Select(v => new SecretVersionInfo
                {
                    Version = v.Version.ToString(),
                    CreatedAt = v.CreatedAt,
                    IsCurrent = v.IsCurrent
                })
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException and not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Conjur error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Conjur without retrieving its value.
    /// </summary>
    /// <param name="key">The Conjur secret path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Conjur API call fails.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.SecretExistsAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SecretProviderException(
                $"Conjur error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    private async Task<ConjurVersionResult?> LatestVersionAsync(string key, CancellationToken cancellationToken)
    {
        var versions = await _client.ListVersionsAsync(key, cancellationToken);
        return versions.OrderByDescending(v => v.Version).FirstOrDefault();
    }

    private async Task<ConjurVersionResult?> FindVersionAsync(
        string key, int version, CancellationToken cancellationToken)
    {
        var versions = await _client.ListVersionsAsync(key, cancellationToken);
        return versions.FirstOrDefault(v => v.Version == version);
    }

    /// <summary>
    /// Disposes the underlying Conjur API client.
    /// </summary>
    public async ValueTask DisposeAsync() =>
        await _client.DisposeAsync();
}