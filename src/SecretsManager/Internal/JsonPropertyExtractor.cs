using System.Text.Json;

namespace SecretsManager.Internal;

/// <summary>
/// Extracts a dot-separated property path from a JSON string.
/// </summary>
public static class JsonPropertyExtractor
{
    /// <summary>
    /// Extracts a nested property from a JSON secret value.
    /// </summary>
    /// <param name="json">The JSON document to read from.</param>
    /// <param name="propertyPath">
    /// Dot-separated path to the property, for example <c>database.password</c>.
    /// </param>
    /// <returns>
    /// The value at the given path: strings are returned as-is, numbers and booleans as their
    /// raw JSON text, and objects or arrays as raw JSON.
    /// </returns>
    /// <exception cref="SecretProviderException">
    /// Thrown when <paramref name="json"/> is not valid JSON, when an intermediate segment is not
    /// an object, or when a segment is missing.
    /// </exception>
    public static string Extract(string json, string propertyPath)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new SecretProviderException(
                $"Cannot extract property '{propertyPath}': secret value is not valid JSON.", ex);
        }

        using (doc)
        {
            var segments = propertyPath.Split('.');
            var current = doc.RootElement;

            foreach (var segment in segments)
            {
                if (current.ValueKind != JsonValueKind.Object)
                    throw new SecretProviderException(
                        $"Cannot extract property '{propertyPath}': " +
                        $"segment '{segment}' is not an object (found {current.ValueKind}).");

                if (!current.TryGetProperty(segment, out var next))
                    throw new SecretProviderException(
                        $"Property '{propertyPath}' not found in secret value " +
                        $"(missing segment '{segment}').");

                current = next;
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString()!,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                    current.GetRawText(),
                _ => current.GetRawText() // objects, arrays → raw JSON
            };
        }
    }
}
