namespace SecretsManager.Internal;

internal interface IProviderNameAwareLayer
{
    void SetProviderName(string providerName);
}
