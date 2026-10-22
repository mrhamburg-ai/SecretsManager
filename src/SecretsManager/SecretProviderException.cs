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
    public SecretProviderException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Wraps a backend failure in a <see cref="SecretProviderException"/>, preserving
    /// cancellation by rethrowing the original <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="exception">The exception raised by the backend.</param>
    /// <returns>A provider exception when the failure is not a cancellation.</returns>
    public static Exception Wrap(string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (FindCancellation(exception) is { } cancellation)
            ExceptionDispatchInfo.Capture(cancellation).Throw();

        return new SecretProviderException(message, exception);
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
