using SecretsManager.Builder;

namespace SecretsManager.Vault;

/// <summary>
/// Factory that creates <see cref="VaultSecretProvider"/> instances from a flat settings dictionary.
/// </summary>
public sealed class VaultSecretProviderFactory : ISecretProviderFactory
{
    /// <summary>
    /// The provider name used in YAML or environment configuration.
    /// </summary>
    public string ProviderName => "vault";

    /// <summary>
    /// Creates a <see cref="VaultSecretProvider"/> from the provided configuration settings.
    /// </summary>
    /// <param name="configuration">A dictionary of settings with keys: <c>url</c>, <c>token</c>, <c>mount.path</c>, and optionally <c>skip.tls.verify</c>.</param>
    /// <returns>A configured <see cref="VaultSecretProvider"/> instance.</returns>
    public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
    {
        var options = MapConfiguration(configuration);
        return new VaultSecretProvider(options);
    }

    internal static VaultOptions MapConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var options = new VaultOptions();

        if (configuration.TryGetValue("url", out var url) && !string.IsNullOrWhiteSpace(url))
            options.Url = url;

        if (configuration.TryGetValue("token", out var token) && !string.IsNullOrWhiteSpace(token))
            options.Token = token;

        if (configuration.TryGetValue("mount.path", out var mountPath) && !string.IsNullOrWhiteSpace(mountPath))
            options.MountPath = mountPath;

        if (configuration.TryGetValue("skip.tls.verify", out var skipVerify) && !string.IsNullOrWhiteSpace(skipVerify))
            options.SkipTlsVerify = bool.TryParse(skipVerify, out var parsed) && parsed;

        return options;
    }
}
