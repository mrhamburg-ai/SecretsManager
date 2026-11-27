using System.Runtime.CompilerServices;
using Azure.Security.KeyVault.Secrets;

namespace SecretsManager.AzureKeyVault.Internal;

internal sealed class SecretClientWrapper : ISecretClientWrapper
{
    private readonly SecretClient _client;

    public SecretClientWrapper(SecretClient client) => _client = client;

    public async Task<KeyVaultSecretResult> GetSecretAsync(
        string name, string? version, CancellationToken cancellationToken)
    {
        var response = await _client.GetSecretAsync(name, version, cancellationToken).ConfigureAwait(false);
        var secret = response.Value;
        return MapToResult(secret);
    }

    public async Task<KeyVaultSecretResult> SetSecretAsync(
        string name, string value, string? contentType, IDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        var kvSecret = new KeyVaultSecret(name, value);

        if (contentType is not null)
            kvSecret.Properties.ContentType = contentType;

        if (tags is not null)
        {
            foreach (var kvp in tags)
                kvSecret.Properties.Tags[kvp.Key] = kvp.Value;
        }

        var response = await _client.SetSecretAsync(kvSecret, cancellationToken).ConfigureAwait(false);
        return MapToResult(response.Value);
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken)
    {
        var operation = await _client.StartDeleteSecretAsync(name, cancellationToken).ConfigureAwait(false);
        await operation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<SecretVersionProperties> GetSecretVersionsAsync(
        string name, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sp in _client.GetPropertiesOfSecretVersionsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            yield return new SecretVersionProperties(sp.Version, sp.CreatedOn, sp.Enabled);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static KeyVaultSecretResult MapToResult(KeyVaultSecret secret)
    {
        var tags = secret.Properties.Tags.Count > 0
            ? new Dictionary<string, string>(secret.Properties.Tags)
            : null;

        return new KeyVaultSecretResult(
            secret.Name,
            secret.Value,
            secret.Properties.Version,
            secret.Properties.CreatedOn,
            secret.Properties.UpdatedOn,
            secret.Properties.ContentType,
            tags);
    }
}
