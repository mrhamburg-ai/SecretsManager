namespace SecretsManager;

/// <summary>
/// Base exception for errors originating from a secret provider backend.
/// </summary>
public class SecretProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretProviderException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SecretProviderException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretProviderException"/> class
    /// with the backend error that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public SecretProviderException(string message, Exception innerException)
        : base(message, innerException) { }
}
