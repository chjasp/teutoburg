// SpellCaster.cs
using UnityEngine;
using Teutoburg.Health;

public class SpellCaster : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform handSocket;   // where the orb spawns

    [Header("Tuning")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float spawnForwardOffset = 0.12f; // in front of the hand
    [SerializeField] private int baseDamage = 50;               // baseline damage when no steps
    [SerializeField] private float stepsToDamageFactor = 0.01f; // e.g., 10k steps => +100 damage

    // Called by Animation Event
    public void SpawnProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("SpellCaster: No projectilePrefab assigned.");
            return;
        }

        // Use the player's facing at the exact frame of the event
        Vector3 dir = transform.forward;

        // Spawn position = hand socket if provided, otherwise in front of the player chest/head area
        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.LookRotation(dir);

        if (handSocket != null)
        {
            spawnPos = handSocket.position + handSocket.forward * spawnForwardOffset;
        }
        else
        {
            // Fallback if you didn't assign a socket:
            spawnPos = transform.position + Vector3.up * 1.4f + dir * 0.5f;
        }

        var go = Instantiate(projectilePrefab, spawnPos, spawnRot);
        var p = go.GetComponent<Projectile>();
        if (p != null)
        {
            p.Init(dir, projectileSpeed);
            p.SetDamage(CalculateDamageFromSteps());
        }
    }

    private int CalculateDamageFromSteps()
    {
        // Ensure a request is in-flight at least once per app session
        if (HKStepsBridge.YesterdaySteps < 0)
        {
            HKStepsBridge.RequestYesterdaySteps();
        }

        long steps = HKStepsBridge.YesterdaySteps;
        if (steps < 0)
        {
            // Not yet available or failed; return baseline
            return baseDamage;
        }

        // Linear scaling: base + factor * steps
        float scaled = baseDamage + (float)steps * stepsToDamageFactor;
        // Clamp to a reasonable range to avoid absurd values
        int finalDamage = Mathf.Clamp(Mathf.RoundToInt(scaled), 0, 100000);
        return finalDamage;
    }
}
