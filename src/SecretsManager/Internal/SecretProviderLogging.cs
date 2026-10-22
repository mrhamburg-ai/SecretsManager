namespace SecretsManager.Internal;

internal static class SecretProviderLogging
{
    public static void TryLog(Action log)
    {
        try
        {
            log();
        }
        catch
        {
            // A broken log sink must never affect a secret operation.
        }
    }
}
