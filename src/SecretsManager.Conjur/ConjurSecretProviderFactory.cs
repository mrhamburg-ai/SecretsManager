using SecretsManager.Builder;

namespace SecretsManager.Conjur;

/// <summary>
/// Factory that creates a <see cref="ConjurSecretProvider"/> from a flat configuration dictionary.
/// Used by <see cref="SecretProviderBuilder"/> when the "conjur" provider is selected.
/// </summary>
public sealed class ConjurSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML, environment variables and the builder API: <c>conjur</c>.
    /// </summary>
    public string ProviderName => "conjur";

    /// <summary>
    /// Creates a <see cref="ConjurSecretProvider"/> from the given settings, reading the
    /// <c>url</c>, <c>account</c>, <c>login</c>, <c>apikey</c>, <c>policy.path</c> and
    /// <c>skip.tls.verify</c> keys.
    /// </summary>
    /// <param name="configuration">Flat provider settings.</param>
    /// <returns>The configured provider.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new ConjurSecretProvider(options);
    }

    internal static ConjurOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new ConjurOptions();

        if (configuration.TryGetValue("url", out var url) && !string.IsNullOrWhiteSpace(url))
            options.Url = url;

        if (configuration.TryGetValue("account", out var account) && !string.IsNullOrWhiteSpace(account))
            options.Account = account;

        if (configuration.TryGetValue("login", out var login) && !string.IsNullOrWhiteSpace(login))
            options.Login = login;

        if (configuration.TryGetValue("apikey", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
            options.ApiKey = apiKey;

        if (configuration.TryGetValue("policy.path", out var policyPath) && !string.IsNullOrWhiteSpace(policyPath))
            options.PolicyPath = policyPath;

        if (configuration.TryGetValue("skip.tls.verify", out var skipVerify) && !string.IsNullOrWhiteSpace(skipVerify))
            options.SkipTlsVerify = bool.TryParse(skipVerify, out var parsed) && parsed;

        return options;
    }
}