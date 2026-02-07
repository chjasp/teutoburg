/// <summary>
/// Provides a common hook for scaling enemy attack damage by tier/level.
/// </summary>
public interface IEnemyAttackTuning
{
    /// <summary>
    /// Returns the prefab/base damage used for scaling.
    /// </summary>
    int BaseAttackDamage { get; }

    /// <summary>
    /// Applies the scaled attack damage value.
    /// </summary>
    /// <param name="value">Scaled damage value to apply.</param>
    void SetAttackDamage(int value);
}
