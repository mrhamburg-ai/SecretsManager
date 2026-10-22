namespace SecretsManager.Internal;

/// <summary>
/// Stand-in for an exception whose text contained a submitted secret value.
/// It keeps the redacted message and the original type name, and drops the inner exception and stack trace.
/// </summary>
internal sealed class RedactedSecretProviderException : Exception
{
    public RedactedSecretProviderException(Exception original, string secretValue)
        : base(original.Message.Replace(secretValue, "[REDACTED]", StringComparison.Ordinal))
    {
        OriginalTypeName = original.GetType().FullName ?? original.GetType().Name;
    }

    public string OriginalTypeName { get; }
}
