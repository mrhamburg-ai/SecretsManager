namespace SecretsManager;

/// <summary>
/// Thrown when a requested secret does not exist in the backend store.
/// </summary>
public class SecretNotFoundException : SecretProviderException
{
    /// <summary>
    /// The key of the secret that could not be found.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The key of the secret that could not be found.</param>
    public SecretNotFoundException(string key)
        : base($"Secret '{key}' was not found.")
    {
        Key = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretNotFoundException"/> class
    /// with the backend error that caused the lookup to fail.
    /// </summary>
    /// <param name="key">The key of the secret that could not be found.</param>
    /// <param name="innerException">The exception that caused the lookup to fail.</param>
    public SecretNotFoundException(string key, Exception innerException)
        : base($"Secret '{key}' was not found.", innerException)
    {
        Key = key;
    }
}
