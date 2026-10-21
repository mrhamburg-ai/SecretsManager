namespace SecretsManager;

/// <summary>
/// Default <see cref="ISecretActorAccessor"/> that flows the actor through
/// <see cref="AsyncLocal{T}"/>, so it follows async continuations.
/// </summary>
public sealed class AmbientSecretActorAccessor : ISecretActorAccessor
{
    /// <summary>
    /// The process-wide ambient accessor used by <see cref="AuditLayerOptions.ActorAccessor"/>
    /// and by hosts that push an actor per request.
    /// </summary>
    public static AmbientSecretActorAccessor Instance { get; } = new();

    private readonly AsyncLocal<SecretActor?> _current = new();

    /// <inheritdoc />
    public SecretActor? Current => _current.Value;

    /// <inheritdoc />
    public IDisposable Push(SecretActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var previous = _current.Value;
        _current.Value = actor;
        return new Scope(this, previous);
    }

    private sealed class Scope(AmbientSecretActorAccessor accessor, SecretActor? previous) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                accessor._current.Value = previous;
        }
    }
}
