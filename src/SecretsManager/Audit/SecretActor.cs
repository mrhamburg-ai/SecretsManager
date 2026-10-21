namespace SecretsManager;

/// <summary>
/// Identity that performs secret operations.
/// </summary>
/// <param name="Id">Stable identifier, for example a service name or subject claim.</param>
/// <param name="DisplayName">Optional human-readable name.</param>
public sealed record SecretActor(string Id, string? DisplayName = null);
