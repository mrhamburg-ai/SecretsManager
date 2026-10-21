namespace SecretsManager;

/// <summary>
/// Ambient accessor for the identity that is performing secret operations.
/// </summary>
public interface ISecretActorAccessor
{
    /// <summary>
    /// The actor in scope, or null when no actor has been pushed.
    /// </summary>
    SecretActor? Current { get; }

    /// <summary>
    /// Pushes an actor onto the ambient scope. Disposing the returned value restores the
    /// previous actor.
    /// </summary>
    /// <param name="actor">The actor to make current.</param>
    /// <returns>A disposable scope that restores the previous actor.</returns>
    IDisposable Push(SecretActor actor);
}
