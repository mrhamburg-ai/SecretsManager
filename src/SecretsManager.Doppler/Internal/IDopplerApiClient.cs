namespace SecretsManager.Doppler.Internal;

internal record DopplerSecretResult(string Value);

internal interface IDopplerApiClient : IAsyncDisposable
{
    Task<DopplerSecretResult?> GetSecretAsync(string key, CancellationToken cancellationToken);

    Task<DopplerSecretResult> PutSecretAsync(
        string key, string value, CancellationToken cancellationToken);

    Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken);

    Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken);
}
