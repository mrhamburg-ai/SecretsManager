namespace SecretsManager.Internal;

internal interface ISecretProviderLoggerAwareLayer
{
    void SetLogger(ISecretProviderLogger logger);
}
