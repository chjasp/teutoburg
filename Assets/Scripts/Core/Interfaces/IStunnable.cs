/// <summary>
/// Common interface for enemies that can be stunned by crowd control effects.
/// </summary>
public interface IStunnable
{
    /// <summary>
    /// Gets whether the actor is currently stunned.
    /// </summary>
    bool IsStunned { get; }

    /// <summary>
    /// Applies a stun for the provided duration in seconds.
    /// </summary>
    /// <param name="seconds">Stun duration in seconds.</param>
    void Stun(float seconds);
}
