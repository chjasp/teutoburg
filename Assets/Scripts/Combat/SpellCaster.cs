using Teutoburg.Combat;
using Teutoburg.Health;
using UnityEngine;

public class SpellCaster : AnimationDrivenAbility
{
    [Header("Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform handSocket;   // where the orb spawns

    [Header("Tuning")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float spawnForwardOffset = 0.12f; // in front of the hand
    [SerializeField] private int baseDamage = 50;               // baseline damage when no steps
    [SerializeField] private float stepsToDamageFactor = 0.01f; // e.g., 10k steps => +100 damage

    protected override void Awake()
    {
        base.Awake();
        EnsureTriggerName("CastSpell");
    }

    public void RequestCast()
    {
        Perform();
    }

    /// <summary>
    /// Animation Event hook used by the cast animation to spawn the projectile.
    /// </summary>
    public void SpawnProjectile()
    {
        ExecuteFromAnimationEvent();
    }

    protected override void Execute()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("SpellCaster: No projectilePrefab assigned.");
            return;
        }

        Vector3 dir = transform.forward;

        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.LookRotation(dir);

        if (handSocket != null)
        {
            spawnPos = handSocket.position + handSocket.forward * spawnForwardOffset;
        }
        else
        {
            spawnPos = transform.position + Vector3.up * 1.4f + dir * 0.5f;
        }

        var go = Instantiate(projectilePrefab, spawnPos, spawnRot);
        var projectile = go.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Init(dir, projectileSpeed);
            projectile.SetDamage(CalculateDamageFromSteps());
        }
    }

    private int CalculateDamageFromSteps()
    {
        if (HKStepsBridge.YesterdaySteps < 0)
        {
            HKStepsBridge.RequestYesterdaySteps();
        }

        long steps = HKStepsBridge.YesterdaySteps;
        if (steps < 0)
        {
            return baseDamage;
        }

        float scaled = baseDamage + (float)steps * stepsToDamageFactor;
        int finalDamage = Mathf.Clamp(Mathf.RoundToInt(scaled), 0, 100000);
        return finalDamage;
    }
}
