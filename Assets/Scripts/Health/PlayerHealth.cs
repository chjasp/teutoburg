using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : HealthBase
{
    [Header("Player Health")]
    [SerializeField] private float hitInvulnerability = 0.25f; // short grace period between hits
    [SerializeField] private GameObject hitVfxPrefab;

    private float invulnerableUntil;

    protected override void Awake()
    {
        base.Awake();
        invulnerableUntil = -999f;
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
        var mover = GetComponent<TopDownMover>();
        if (mover != null) mover.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
    }

    protected override void OnDeathFinalize()
    {
        // Player: keep object alive by default (no destroy)
    }
}


