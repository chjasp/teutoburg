using System;
using UnityEngine;
using Axiom.Core;

[DisallowMultipleComponent]
public class PlayerHealth : HealthBase
{
    /// <summary>
    /// Optional external death handler. Return true to suppress default run reset behavior.
    /// </summary>
    public static Func<PlayerHealth, bool> DeathHandledExternally;

    [Header("Player Health")]
    [SerializeField] private float hitInvulnerability = 0.25f; // short grace period between hits
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private PlayerStatusEffects _statusEffects;

    private float invulnerableUntil;

    protected override void Awake()
    {
        base.Awake();
        if (_statusEffects == null)
        {
            _statusEffects = GetComponent<PlayerStatusEffects>();
        }
        invulnerableUntil = -999f;
    }

    public override void TakeDamage(int amount)
    {
        float multiplier = _statusEffects != null ? _statusEffects.DamageTakenMultiplier : 1f;
        int adjusted = Mathf.RoundToInt(amount * multiplier);
        base.TakeDamage(adjusted);
    }

    protected override bool CanTakeDamageNow()
    {
        return base.CanTakeDamageNow() && Time.time >= invulnerableUntil;
    }

    protected override void OnAfterDamageTaken(int damageAmount)
    {
        invulnerableUntil = Time.time + hitInvulnerability;
        if (hitVfxPrefab != null)
        {
            Instantiate(hitVfxPrefab, transform.position + Vector3.up * 1.2f, Quaternion.identity);
        }
    }

    protected override void OnDeathStart()
    {
        // Disable movement and attacks
        var mover = GetComponent<TopDownMover>();
        if (mover != null) mover.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        var melee = GetComponent<PlayerMelee>();
        if (melee != null) melee.enabled = false;
        
        // Disable ability scripts
        foreach (var ability in GetComponents<MonoBehaviour>())
        {
            if (ability is Heartfire || ability.GetType().Name.Contains("Ability"))
                ability.enabled = false;
        }
    }

    protected override void OnDeathFinalize()
    {
        if (DeathHandledExternally != null && DeathHandledExternally(this))
        {
            return;
        }

        GameManager.Instance.ResetRun();
    }
}
