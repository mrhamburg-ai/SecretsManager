namespace SecretsManager;

/// <summary>
/// Destination for audit records. Implementations must be thread-safe.
/// </summary>
public interface ISecretAuditSink
{
    /// <summary>
    /// Writes a single audit record. Called once per audited operation.
    /// </summary>
    /// <param name="auditEvent">The immutable audit record.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes when the record has been accepted by the sink.</returns>
    ValueTask WriteAsync(SecretAuditEvent auditEvent, CancellationToken cancellationToken = default);
}
