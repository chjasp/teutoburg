/// <summary>
/// Allows enemies to be forced into an aggressive state when hit.
/// </summary>
public interface IEnemyAggro
{
    /// <summary>
    /// Forces the enemy to immediately aggro the player.
    /// </summary>
    void ForceAggro();
}
