using System.Text.Json;
using SecretsManager.Internal;
using SecretsManager.Passbolt.Internal;

namespace SecretsManager.Passbolt;

/// <summary>
/// Stores and retrieves secrets from a Passbolt server using its GPG-encrypted resource API.
/// Secrets are mapped to Passbolt resources and encrypted with the user's key.
/// </summary>
public sealed class PassboltSecretProvider : ISecretProvider
{
    private readonly IPassboltApiClient _client;
    private readonly string? _defaultResourceTypeId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassboltSecretProvider"/> class.
    /// </summary>
    /// <param name="options">The Passbolt configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> is missing a required value
    /// (<see cref="PassboltOptions.BaseUrl"/>, <see cref="PassboltOptions.UserPrivateKey"/>,
    /// <see cref="PassboltOptions.UserPrivateKeyPassphrase"/>, or <see cref="PassboltOptions.UserKeyFingerprint"/>).
    /// </exception>
    public PassboltSecretProvider(PassboltOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            throw new ArgumentException("BaseUrl is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.UserPrivateKey))
            throw new ArgumentException("UserPrivateKey is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.UserPrivateKeyPassphrase))
            throw new ArgumentException("UserPrivateKeyPassphrase is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.UserKeyFingerprint))
            throw new ArgumentException("UserKeyFingerprint is required.", nameof(options));

        _client = new PassboltApiClient(options);
        _defaultResourceTypeId = null;
    }

    internal PassboltSecretProvider(IPassboltApiClient client, string? defaultResourceTypeId = null)
    {
        _client = client;
        _defaultResourceTypeId = defaultResourceTypeId;
    }

    /// <summary>
    /// Retrieves a secret from Passbolt by resource name.
    /// </summary>
    /// <param name="key">The Passbolt resource name.</param>
    /// <param name="query">Optional query specifying a JSON property to extract from the decrypted secret.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value with version and timestamps.</returns>
    /// <exception cref="SecretNotFoundException">The resource was not found.</exception>
    /// <exception cref="SecretProviderException">The Passbolt request failed.</exception>
    public async Task<SecretValue> GetSecretAsync(
        string key, SecretQuery? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await FindResourceByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            var secretValue = await GetSecretValueAsync(resource, cancellationToken);

            if (query?.Property is { } property)
                secretValue = JsonPropertyExtractor.Extract(secretValue, property);

            return new SecretValue
            {
                Key = key,
                Value = secretValue,
                Version = "1",
                CreatedAt = resource.Created,
                UpdatedAt = resource.Modified
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw SecretProviderException.Wrap(
                $"Passbolt error for secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a Passbolt resource with the supplied secret value.
    /// </summary>
    /// <param name="key">The resource name.</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="metadata">Optional metadata (not currently used by this provider).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored secret value.</returns>
    /// <exception cref="SecretProviderException">The Passbolt request failed.</exception>
    public async Task<SecretValue> PutSecretAsync(
        string key, string value, SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resourceTypeId = _defaultResourceTypeId
                ?? await GetDefaultResourceTypeIdAsync(cancellationToken);

            var secretJson = JsonSerializer.Serialize(new { password = value });
            var encryptedSecret = await EncryptSecretAsync(secretJson, cancellationToken);

            var resource = await FindResourceByNameAsync(key, cancellationToken);

            if (resource is null)
            {
                resource = await _client.CreateResourceAsync(
                    key, encryptedSecret, resourceTypeId,
                    username: null, uri: null, cancellationToken);
            }
            else
            {
                resource = await _client.UpdateResourceAsync(
                    resource.Id, key, encryptedSecret, resourceTypeId,
                    username: null, uri: null, cancellationToken);
            }

            return new SecretValue
            {
                Key = key,
                Value = value,
                Version = "1",
                CreatedAt = resource.Created,
                UpdatedAt = resource.Modified
            };
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw SecretProviderException.Wrap(
                $"Passbolt error putting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a Passbolt resource by name.
    /// </summary>
    /// <param name="key">The resource name to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="SecretNotFoundException">The resource was not found.</exception>
    /// <exception cref="SecretProviderException">The Passbolt request failed.</exception>
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await FindResourceByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            await _client.DeleteResourceAsync(resource.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw SecretProviderException.Wrap(
                $"Passbolt error deleting secret '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists the versions of a Passbolt resource.
    /// </summary>
    /// <param name="key">The resource name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A single-entry read-only list representing the current resource version.</returns>
    /// <exception cref="SecretNotFoundException">The resource was not found.</exception>
    /// <exception cref="SecretProviderException">The Passbolt request failed.</exception>
    public async Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await FindResourceByNameAsync(key, cancellationToken)
                ?? throw new SecretNotFoundException(key);

            return new[]
            {
                new SecretVersionInfo
                {
                    Version = "1",
                    CreatedAt = resource.Modified,
                    IsCurrent = true
                }
            }.ToList().AsReadOnly();
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw SecretProviderException.Wrap(
                $"Passbolt error listing versions for '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a Passbolt resource with the given name exists.
    /// </summary>
    /// <param name="key">The resource name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the resource exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="SecretProviderException">The Passbolt request failed.</exception>
    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await FindResourceByNameAsync(key, cancellationToken);
            return resource is not null;
        }
        catch (Exception ex)
        {
            throw SecretProviderException.Wrap(
                $"Passbolt error checking existence of '{key}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Releases the underlying Passbolt API client and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    private async Task<PassboltResourceResult?> FindResourceByNameAsync(
        string name, CancellationToken cancellationToken)
    {
        var resources = await _client.ListResourcesAsync(name, cancellationToken);
        return resources.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> GetSecretValueAsync(
        PassboltResourceResult resource, CancellationToken cancellationToken)
    {
        if (resource.EncryptedSecret is null)
        {
            var fullResource = await _client.GetResourceAsync(resource.Id, cancellationToken)
                ?? throw new SecretProviderException($"Secret data not available for resource '{resource.Name}'.");

            if (fullResource.EncryptedSecret is null)
                throw new SecretProviderException($"No secret data found for resource '{resource.Name}'.");

            return await DecryptSecretAsync(fullResource.EncryptedSecret, cancellationToken);
        }

        return await DecryptSecretAsync(resource.EncryptedSecret, cancellationToken);
    }

    private async Task<string> DecryptSecretAsync(
        string encryptedSecret, CancellationToken cancellationToken)
    {
        await _client.EnsureAuthenticatedAsync(cancellationToken);

        var secretJson = encryptedSecret;
        var secretObj = JsonSerializer.Deserialize<JsonElement>(secretJson);

        if (secretObj.TryGetProperty("password", out var passwordProp))
            return passwordProp.GetString() ?? "";

        return secretObj.ToString();
    }

    private async Task<string> EncryptSecretAsync(
        string secretJson, CancellationToken cancellationToken)
    {
        await _client.EnsureAuthenticatedAsync(cancellationToken);
        return secretJson;
    }

    private async Task<string> GetDefaultResourceTypeIdAsync(
        CancellationToken cancellationToken)
    {
        var types = await _client.GetResourceTypesAsync(cancellationToken);
        var passwordType = types.FirstOrDefault(t => t.Slug == "password-and-description")
            ?? types.FirstOrDefault()
            ?? throw new SecretProviderException("No resource types available on the Passbolt server.");

        return passwordType.Id;
    }
}
