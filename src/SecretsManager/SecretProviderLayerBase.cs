namespace SecretsManager;

/// <summary>
/// Base class for layers that wrap every <see cref="ISecretProvider"/> operation at one
/// interception point.
/// </summary>
/// <remarks>
/// Derived layers override <see cref="ExecuteAsync{TResult}"/> instead of each of the five
/// interface methods. <see cref="DisposeAsync"/> forwards to the wrapped provider by default.
/// </remarks>
public abstract class SecretProviderLayerBase : ISecretProvider
{
    /// <summary>
    /// Initializes the layer around the provider it decorates.
    /// </summary>
    /// <param name="inner">The provider to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    protected SecretProviderLayerBase(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    /// <summary>
    /// Gets the provider this layer wraps.
    /// </summary>
    protected ISecretProvider Inner { get; }

    /// <summary>
    /// Retrieves a secret, forwarding through <see cref="ExecuteAsync{TResult}"/>.
    /// </summary>
    public Task<SecretValue> GetSecretAsync(
        string key,
        SecretQuery? query = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "GetSecretAsync",
            key,
            token => Inner.GetSecretAsync(key, query, token),
            cancellationToken);

    /// <summary>
    /// Creates or updates a secret, forwarding through <see cref="ExecuteAsync{TResult}"/>.
    /// </summary>
    public Task<SecretValue> PutSecretAsync(
        string key,
        string value,
        SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "PutSecretAsync",
            key,
            token => Inner.PutSecretAsync(key, value, metadata, token),
            cancellationToken);

    /// <summary>
    /// Deletes a secret, forwarding through <see cref="ExecuteAsync{TResult}"/>.
    /// </summary>
    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "DeleteSecretAsync",
            key,
            async token =>
            {
                await Inner.DeleteSecretAsync(key, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    /// <summary>
    /// Lists secret versions, forwarding through <see cref="ExecuteAsync{TResult}"/>.
    /// </summary>
    public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
        string key,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "GetSecretVersionsAsync",
            key,
            token => Inner.GetSecretVersionsAsync(key, token),
            cancellationToken);

    /// <summary>
    /// Checks whether a secret exists, forwarding through <see cref="ExecuteAsync{TResult}"/>.
    /// </summary>
    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "SecretExistsAsync",
            key,
            token => Inner.SecretExistsAsync(key, token),
            cancellationToken);

    /// <summary>
    /// Runs one provider operation. <paramref name="operation"/> is the interface method name and
    /// <paramref name="key"/> is the secret key the caller supplied.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="operation">The interface method name, for example <c>GetSecretAsync</c>.</param>
    /// <param name="key">The secret key supplied by the caller.</param>
    /// <param name="operationExecutor">Invokes the wrapped provider.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the wrapped operation.</returns>
    protected abstract Task<TResult> ExecuteAsync<TResult>(
        string operation,
        string key,
        Func<CancellationToken, Task<TResult>> operationExecutor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Disposes the wrapped provider.
    /// </summary>
    public virtual ValueTask DisposeAsync() => Inner.DisposeAsync();
}
