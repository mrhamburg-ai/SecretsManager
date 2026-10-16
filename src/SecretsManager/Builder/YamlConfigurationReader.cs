using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SecretsManager.Builder;

/// <summary>
/// Parses a YAML document (modeled after External Secrets Operator's SecretStore spec)
/// into a <see cref="SecretProviderConfiguration"/>.
///
/// Expected format:
/// <code>
/// provider:
///   filesystem:
///     path: /secrets
///     encryption:
///       key: base64key
/// </code>
/// </summary>
public static class YamlConfigurationReader
{
    /// <summary>
    /// Parses a YAML document into a <see cref="SecretProviderConfiguration"/>.
    /// </summary>
    /// <param name="yaml">The YAML document to parse.</param>
    /// <returns>The provider name and its flattened settings.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the document is empty, has no top-level <c>provider</c> block, or declares
    /// anything other than exactly one provider.
    /// </exception>
    public static SecretProviderConfiguration Read(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var root = deserializer.Deserialize<Dictionary<string, object>>(yaml)
            ?? throw new InvalidOperationException("YAML document is empty.");

        if (!root.TryGetValue("provider", out var providerObj) || providerObj is not Dictionary<object, object> providers)
            throw new InvalidOperationException("YAML must contain a top-level 'provider' key with a provider block.");

        if (providers.Count != 1)
            throw new InvalidOperationException(
                $"Exactly one provider must be specified under 'provider'. Found {providers.Count}.");

        var entry = providers.First();
        var providerName = entry.Key.ToString()!;
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (entry.Value is Dictionary<object, object> providerSettings)
            Flatten(providerSettings, prefix: "", settings);

        return new SecretProviderConfiguration
        {
            ProviderName = providerName,
            Settings = settings
        };
    }

    private static void Flatten(Dictionary<object, object> source, string prefix, Dictionary<string, string> target)
    {
        foreach (var kvp in source)
        {
            var key = string.IsNullOrEmpty(prefix)
                ? kvp.Key.ToString()!
                : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<object, object> nested)
                Flatten(nested, key, target);
            else if (kvp.Value is not null)
                target[key] = kvp.Value.ToString()!;
        }
    }
}
