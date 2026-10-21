namespace SecretsManager;

/// <summary>
/// Options for <see cref="AuditLayer"/>.
/// </summary>
public sealed class AuditLayerOptions
{
    /// <summary>
    /// Sinks that receive every audit event. At least one is required when the layer is enabled.
    /// </summary>
    public IList<ISecretAuditSink> Sinks { get; } = new List<ISecretAuditSink>();

    /// <summary>
    /// Provider name recorded on every event. The builder supplies this at build time.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Which operations are audited. Defaults to <see cref="SecretAuditScope.All"/>.
    /// </summary>
    public SecretAuditScope Scope { get; set; } = SecretAuditScope.All;

    /// <summary>
    /// What to do when a sink throws. Defaults to <see cref="AuditFailureMode.Ignore"/>.
    /// </summary>
    public AuditFailureMode OnSinkFailure { get; set; } = AuditFailureMode.Ignore;

    /// <summary>
    /// Invoked when a sink failure is ignored: always when the secret operation already failed, and
    /// when <see cref="OnSinkFailure"/> is <see cref="AuditFailureMode.Ignore"/>. In
    /// <see cref="AuditFailureMode.Throw"/> mode a successful operation rethrows the sink failure
    /// after every sink received the record. Exceptions thrown by this callback are ignored.
    /// </summary>
    public Action<Exception>? OnSinkError { get; set; }

    /// <summary>
    /// Actor lookup. Defaults to <see cref="AmbientSecretActorAccessor.Instance"/>.
    /// </summary>
    public ISecretActorAccessor? ActorAccessor { get; set; } = AmbientSecretActorAccessor.Instance;

    /// <summary>
    /// Whether the layer is active. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Validates the options and returns a collection of error messages.
    /// </summary>
    /// <returns>Validation error messages; empty when the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (!Enabled)
            yield break;

        if (Sinks.Count == 0)
            yield return "At least one audit sink is required";

        if (string.IsNullOrWhiteSpace(ProviderName))
            yield return "ProviderName is required";
    }
}
