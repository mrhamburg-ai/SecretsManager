using System.Collections;

namespace SecretsManager.Builder;

/// <summary>
/// Reads provider configuration from environment variables.
///
/// Convention:
///   {PREFIX}_PROVIDER          → provider name (e.g. "filesystem")
///   {PREFIX}_{PROVIDER}_{KEY}  → setting key, with underscores mapping to dots for nesting
///
/// Example:
///   SECRETS_PROVIDER=filesystem
///   SECRETS_FILESYSTEM_PATH=/var/secrets
///   SECRETS_FILESYSTEM_ENCRYPTION_KEY=base64...
///
/// Produces settings: { "path": "/var/secrets", "encryption.key": "base64..." }
/// </summary>
public static class EnvironmentVariableConfigurationReader
{
    /// <summary>
    /// Reads provider configuration from environment variables.
    /// </summary>
    /// <param name="prefix">Variable prefix to read from. Defaults to <c>SECRETS</c>.</param>
    /// <returns>The provider name and its flattened settings.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <c>{prefix}_PROVIDER</c> variable is not set.
    /// </exception>
    public static SecretProviderConfiguration Read(string prefix = "SECRETS")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var providerName = Environment.GetEnvironmentVariable($"{prefix}_PROVIDER");
        if (string.IsNullOrWhiteSpace(providerName))
            throw new InvalidOperationException(
                $"Environment variable '{prefix}_PROVIDER' is not set. " +
                "It must contain the provider name (e.g. 'filesystem').");

        var settingsPrefix = $"{prefix}_{providerName}_".ToUpperInvariant();
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var envKey = entry.Key.ToString()!;
            if (!envKey.StartsWith(settingsPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var remainder = envKey[settingsPrefix.Length..];
            var settingKey = remainder.ToLowerInvariant().Replace('_', '.');
            settings[settingKey] = entry.Value?.ToString() ?? "";
        }

        return new SecretProviderConfiguration
        {
            ProviderName = providerName,
            Settings = settings
        };
    }
}
