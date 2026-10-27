using SecretsManager.Infisical.Internal;
using SecretsManager.Internal;

namespace SecretsManager.Infisical;

/// <summary>
/// Stores and retrieves secrets in Infisical, authenticating with a machine identity through
/// Universal Auth. Supports numeric version selection and JSON property extraction.
/// </summary>
public sealed class InfisicalSecretProvider : ISecretProvider
{
    private const int MaxVersionsToProbe = 1000;

    private readonly IInfisicalApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfisicalSecretProvider"/> class.
    /// </summary>
    /// <param name="options">Connection and authentication options for Infisical.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the options are invalid.</exception>
    public InfisicalSecretProvider(InfisicalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Invalid Infisical options: {string.Join("; ", errors)}", nameof(options));

        _client = new InfisicalApiClient(options);
    }

    internal InfisicalSecretProvider(IInfisicalApiClient client) =>
        _client = client;

    /// <summary>
    /// Retrieves a secret from Infisical, optionally selecting a numeric version and/or extracting
    /// a JSON property from the stored value.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="query">Optional version and/or JSON property selector.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The secret value, with its version and metadata.</returns>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Infisical API call fails.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            int? version = null;
            if (query?.Version is { } value)
            {
                if (!int.TryParse(value, out var parsed) || parsed < 1)
                    throw new SecretProviderException(
                        $"Invalid Infisical version '{value}'. Version must be a positive integer.");

                version = parsed;
            }

            var secret = await _client.GetSecretAsync(key, version, true, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var secretValue = secret.Value;
            if (query?.Property is { } property)
                secretValue = JsonPropertyExtractor.Extract(secretValue, property);

            return new SecretValue
            {
                Key = key,
                Value = secretValue,
                Version = secret.Version.ToString(),
                Tags = secret.Metadata
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Infisical error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a secret in Infisical and returns the newly created version.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="metadata">Optional metadata. <see cref="SecretMetadata.Tags"/> is stored as
    /// Infisical secret metadata; <see cref="SecretMetadata.ContentType"/> is not supported.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The stored value, with its version and metadata.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Infisical API call fails.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _client.FindSecretAsync(key, cancellationToken);
            var tags = metadata?.Tags;

            var result = existing is null
                ? await _client.CreateSecretAsync(key, value, tags, cancellationToken)
                : await _client.UpdateSecretAsync(key, value, tags, cancellationToken);

            return new SecretValue
            {
                Key = key,
                Value = result.Value,
                Version = result.Version.ToString(),
                Tags = result.Metadata
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Infisical error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret from Infisical.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="SecretNotFoundException">Thrown when the secret does not exist.</exception>
    /// <exception cref="SecretProviderException">Thrown when the Infisical API call fails.</exception>
    public async Task DeleteSecretAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _client.DeleteSecretAsync(key, cancellationToken))
                throw new SecretNotFoundException(key);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Infisical error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists the versions of a secret. Infisical has no version-list endpoint, so the provider reads
    /// the current version and probes versions 1 through the current version. Missing versions are
    /// skipped.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One entry per readable version, with the current version flagged.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Infisical API call fails.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var current = await _client.FindSecretAsync(key, cancellationToken);
            if (current is null)
                return Array.Empty<SecretVersionInfo>();

            if (current.Version > MaxVersionsToProbe)
                throw new SecretProviderException(
                    $"Infisical secret '{key}' has {current.Version} versions, which exceeds the " +
                    $"supported limit of {MaxVersionsToProbe}. Use the Infisical UI or API to " +
                    "inspect older versions.");

            var versions = new List<SecretVersionInfo>(current.Version);
            for (var version = 1; version <= current.Version; version++)
            {
                var secret = await _client.GetSecretAsync(
                    key, version, false, cancellationToken);

                if (secret is null)
                    continue;

                versions.Add(new SecretVersionInfo
                {
                    Version = version.ToString(),
                    IsCurrent = version == current.Version
                });
            }

            return versions.AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Infisical error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a secret exists in Infisical without retrieving its value.
    /// </summary>
    /// <param name="key">The secret name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the secret exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">Thrown when the Infisical API call fails.</exception>
    public async Task<bool> SecretExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.FindSecretAsync(key, cancellationToken) is not null;
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Infisical error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Disposes the underlying Infisical API client.
    /// </summary>
    public async ValueTask DisposeAsync() =>
        await _client.DisposeAsync();
}
