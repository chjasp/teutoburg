using System;
using UnityEngine;
using Axiom.Core;

[DisallowMultipleComponent]
public class EnemyHealth : HealthBase
{
    [Header("Tier")]
    [SerializeField] private EnemyTier tier = EnemyTier.Medium;
    [SerializeField] private bool useAssignedTier = false;

    private int baseMaxHealth;

    public EnemyTier Tier => tier;
    public bool UseAssignedTier => useAssignedTier;
    public EnemyTier AssignedTier => tier;

    protected override void Awake()
    {
        baseMaxHealth = MaxHealth;
        // Apply level-based health multiplier before base.Awake() initializes health
        ApplyLevelHealthMultiplier();
        
        base.Awake();
        
        // Register with LevelManager
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterEnemy();
        }
    }

    public void ApplyTierAndStats(EnemyTier newTier, float levelHealthMultiplier, int level)
    {
        tier = newTier;
        int finalHealth = CombatTuning.GetEnemyMaxHealth(tier, baseMaxHealth, levelHealthMultiplier);
        SetMaxHealth(finalHealth, true);

        var attackTuning = GetComponent<IEnemyAttackTuning>();
        if (attackTuning != null)
        {
            int finalDamage = CombatTuning.GetEnemyAttackDamage(tier, attackTuning.BaseAttackDamage, level);
            attackTuning.SetAttackDamage(finalDamage);
        }
    }

    private void ApplyLevelHealthMultiplier()
    {
        if (LevelManager.Instance != null)
        {
            float multiplier = LevelManager.Instance.GetHealthMultiplier();
            SetMaxHealthMultiplier(multiplier);
        }
    }

    protected override void OnAfterDamageTaken(int damageAmount)
    {
        base.OnAfterDamageTaken(damageAmount);
        // Getting hit should aggro the enemy
        var aggro = GetComponent<IEnemyAggro>();
        if (aggro != null)
        {
            aggro.ForceAggro();
        }
    }

    protected override void OnDeathStart()
    {
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        
        // Unregister from LevelManager
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.UnregisterEnemy();
        }
    }
}
