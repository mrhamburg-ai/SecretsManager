using System.Runtime.ExceptionServices;

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
    /// <remarks>
    /// When <paramref name="innerException"/> represents a cancellation, the original
    /// <see cref="OperationCanceledException"/> is rethrown instead of being wrapped.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The backend failure was caused by cancellation.
    /// </exception>
    public SecretProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
        if (FindCancellation(innerException) is { } cancellation)
            ExceptionDispatchInfo.Capture(cancellation).Throw();
    }

    private static OperationCanceledException? FindCancellation(Exception exception)
    {
        if (exception is OperationCanceledException cancellation)
            return cancellation;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                if (FindCancellation(inner) is { } innerCancellation)
                    return innerCancellation;
            }
        }

        return exception.InnerException is null ? null : FindCancellation(exception.InnerException);
    }
}
