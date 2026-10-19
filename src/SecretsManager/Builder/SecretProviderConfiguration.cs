namespace SecretsManager.Builder;

/// <summary>
/// Intermediate configuration model holding the selected provider name
/// and its flat key-value settings.
/// </summary>
public sealed class SecretProviderConfiguration
{
    /// <summary>
    /// Name of the selected provider, matching an <see cref="ISecretProviderFactory.ProviderName"/>
    /// registered on the builder.
    /// </summary>
    public string ProviderName { get; set; } = "";

    /// <summary>
    /// Flat settings for the selected provider, using dot-separated keys for nesting
    /// (for example <c>encryption.key</c>).
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
