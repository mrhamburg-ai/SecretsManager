using System.Diagnostics;
using SecretsManager.Internal;

namespace SecretsManager;

/// <summary>
/// Emits a durable audit record for every audited provider operation.
/// A sink failure never changes the outcome of the secret operation unless
/// <see cref="AuditLayerOptions.OnSinkFailure"/> is explicitly set to
/// <see cref="AuditFailureMode.Throw"/>.
/// </summary>
public sealed class AuditLayer : ISecretProviderLayer, IProviderNameAwareLayer
{
    private readonly AuditLayerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLayer"/> class.
    /// </summary>
    /// <param name="options">Layer options, including the sinks. Defaults to <see cref="AuditLayerOptions"/>.</param>
    public AuditLayer(AuditLayerOptions? options = null)
    {
        _options = options ?? new AuditLayerOptions();
    }

    internal AuditLayerOptions Options => _options;

    /// <inheritdoc />
    public ISecretProvider Wrap(ISecretProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (!_options.Enabled)
            return inner;

        var errors = _options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Invalid {nameof(AuditLayerOptions)}: {string.Join("; ", errors)}");

        // Snapshot the policy: mutating the options after Build() must not change a built provider.
        return new AuditSecretProvider(
            inner,
            _options.ProviderName!,
            _options.Scope,
            _options.OnSinkFailure,
            _options.OnSinkError,
            _options.ActorAccessor ?? AmbientSecretActorAccessor.Instance,
            _options.Sinks.ToArray());
    }

    void IProviderNameAwareLayer.SetProviderName(string providerName)
    {
        _options.ProviderName = providerName;
    }

    private sealed class AuditSecretProvider(
        ISecretProvider inner,
        string providerName,
        SecretAuditScope scope,
        AuditFailureMode onSinkFailure,
        Action<Exception>? onSinkError,
        ISecretActorAccessor actorAccessor,
        IReadOnlyList<ISecretAuditSink> sinks) : ISecretProvider
    {
        private readonly ISecretProvider _inner = inner;

        public Task<SecretValue> GetSecretAsync(
            string key,
            SecretQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                "GetSecretAsync",
                SecretAuditScope.Reads,
                key,
                () => _inner.GetSecretAsync(key, query, cancellationToken),
                static result => result.Version);
        }

        public Task<SecretValue> PutSecretAsync(
            string key,
            string value,
            SecretMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                "PutSecretAsync",
                SecretAuditScope.Writes,
                key,
                () => _inner.PutSecretAsync(key, value, metadata, cancellationToken),
                static result => result.Version);
        }

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                "DeleteSecretAsync",
                SecretAuditScope.Deletes,
                key,
                () => _inner.DeleteSecretAsync(key, cancellationToken));
        }

        public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                "GetSecretVersionsAsync",
                SecretAuditScope.Enumeration,
                key,
                () => _inner.GetSecretVersionsAsync(key, cancellationToken));
        }

        public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                "SecretExistsAsync",
                SecretAuditScope.Enumeration,
                key,
                () => _inner.SecretExistsAsync(key, cancellationToken));
        }

        // Disposal is only audited when every scope is enabled.
        public ValueTask DisposeAsync()
        {
            return new ValueTask(ExecuteAsync(
                "DisposeAsync",
                SecretAuditScope.All,
                key: null,
                () => _inner.DisposeAsync().AsTask()));
        }

        private async Task ExecuteAsync(
            string operation,
            SecretAuditScope scope,
            string? key,
            Func<Task> operationExecutor)
        {
            await ExecuteAsync(
                operation,
                scope,
                key,
                async () =>
                {
                    await operationExecutor().ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);
        }

        private async Task<TResult> ExecuteAsync<TResult>(
            string operation,
            SecretAuditScope scope,
            string? key,
            Func<Task<TResult>> operationExecutor,
            Func<TResult, string?>? versionSelector = null)
        {
            if (!ShouldAudit(scope))
                return await operationExecutor().ConfigureAwait(false);

            var context = CaptureContext();
            var started = Stopwatch.GetTimestamp();

            TResult result;
            try
            {
                result = await operationExecutor().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await EmitAsync(
                    CreateEvent(
                        operation,
                        key,
                        ex is OperationCanceledException
                            ? SecretAuditOutcome.Cancelled
                            : SecretAuditOutcome.Failure,
                        context,
                        started,
                        null,
                        ex.GetType().FullName),
                    forceIgnoreFailures: true).ConfigureAwait(false);
                throw;
            }

            // Emitted outside the try block so a sink failure (AuditFailureMode.Throw) is not
            // mistaken for an operation failure and recorded a second time.
            await EmitAsync(
                CreateEvent(
                    operation,
                    key,
                    SecretAuditOutcome.Success,
                    context,
                    started,
                    versionSelector?.Invoke(result),
                    null),
                forceIgnoreFailures: false).ConfigureAwait(false);
            return result;
        }

        private bool ShouldAudit(SecretAuditScope operationScope) => (scope & operationScope) == operationScope;

        private AuditContext CaptureContext()
        {
            var activity = Activity.Current;
            return new AuditContext(
                DateTimeOffset.UtcNow,
                actorAccessor.Current,
                activity?.TraceId.ToString(),
                activity?.GetBaggageItem("correlation.id")
                    ?? activity?.GetBaggageItem("correlation-id"));
        }

        private SecretAuditEvent CreateEvent(
            string operation,
            string? key,
            SecretAuditOutcome outcome,
            AuditContext context,
            long started,
            string? version,
            string? failureType)
        {
            return new SecretAuditEvent
            {
                Operation = operation,
                Provider = providerName,
                Key = key,
                Version = version,
                Outcome = outcome,
                Timestamp = context.Timestamp,
                Duration = Stopwatch.GetElapsedTime(started),
                ActorId = context.Actor?.Id,
                ActorDisplayName = context.Actor?.DisplayName,
                CorrelationId = context.CorrelationId,
                TraceId = context.TraceId,
                FailureType = failureType
            };
        }

        private async ValueTask EmitAsync(SecretAuditEvent auditEvent, bool forceIgnoreFailures)
        {
            Exception? firstFailure = null;

            foreach (var sink in sinks)
            {
                try
                {
                    // Deliberately not the caller's token: an audit record must survive cancellation.
                    await sink.WriteAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    firstFailure ??= ex;

                    // Throw mode still delivers the record to every sink before it rethrows.
                    if (forceIgnoreFailures || onSinkFailure == AuditFailureMode.Ignore)
                        NotifySinkError(ex);
                }
            }

            if (!forceIgnoreFailures && onSinkFailure == AuditFailureMode.Throw && firstFailure is not null)
                throw firstFailure;
        }

        private void NotifySinkError(Exception exception)
        {
            try
            {
                onSinkError?.Invoke(exception);
            }
            catch
            {
                // The error callback must never affect the secret operation.
            }
        }

        private sealed record AuditContext(
            DateTimeOffset Timestamp,
            SecretActor? Actor,
            string? TraceId,
            string? CorrelationId);
    }
}
