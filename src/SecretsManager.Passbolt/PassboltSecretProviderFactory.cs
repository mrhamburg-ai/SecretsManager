using SecretsManager.Builder;

namespace SecretsManager.Passbolt;

/// <summary>
/// Factory that creates a <see cref="PassboltSecretProvider"/> from a flat configuration dictionary.
/// Used by <see cref="SecretProviderBuilder"/> when the "passbolt" provider is selected.
/// </summary>
public sealed class PassboltSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML or environment configuration.
    /// </summary>
    public string ProviderName => "passbolt";

    /// <summary>
    /// Creates a <see cref="PassboltSecretProvider"/> from the supplied configuration.
    /// </summary>
    /// <param name="configuration">
    /// The flat settings dictionary. Recognized keys:
    /// <c>base.url</c>, <c>user.private.key</c>, <c>user.private.passphrase</c>,
    /// and <c>user.key.fingerprint</c>.
    /// </param>
    /// <returns>The configured <see cref="PassboltSecretProvider"/>.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new PassboltSecretProvider(options);
    }

    internal static PassboltOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new PassboltOptions();

        if (configuration.TryGetValue("base.url", out var baseUrl) && !string.IsNullOrWhiteSpace(baseUrl))
            options.BaseUrl = baseUrl;

        if (configuration.TryGetValue("user.private.key", out var privateKey) && !string.IsNullOrWhiteSpace(privateKey))
            options.UserPrivateKey = privateKey;

        if (configuration.TryGetValue("user.private.passphrase", out var passphrase) && !string.IsNullOrWhiteSpace(passphrase))
            options.UserPrivateKeyPassphrase = passphrase;

        if (configuration.TryGetValue("user.key.fingerprint", out var fingerprint) && !string.IsNullOrWhiteSpace(fingerprint))
            options.UserKeyFingerprint = fingerprint;

        return options;
    }
}
