// Dynamo.cs
using UnityEngine;
using Axiom.Core;

/// <summary>
/// Dynamo - Fires a charged railgun shot from a wrist-mounted launcher.
/// The weapon has no ammo — it's powered entirely by kinetic energy harvested
/// from your movement via piezoelectric chargers in your suit.
/// More exertion means more charge, more damage.
/// Scales with: Drive
/// </summary>
public class Dynamo : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform handSocket;   // where the shot spawns

    [Header("Tuning")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float spawnForwardOffset = 0.12f; // in front of the hand
    [SerializeField] private int baseDamage = 50;               // baseline damage when no Drive
    [SerializeField] private float driveToDamageFactor = 2.5f;   // e.g., 100 Drive => +250 damage

    // Called by Animation Event
    public void SpawnProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Dynamo: No projectilePrefab assigned.");
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

        Debug.Log($"[Dynamo] Spawning projectile at {spawnPos}, direction {dir}");
        var go = Instantiate(projectilePrefab, spawnPos, spawnRot);
        var p = go.GetComponent<Projectile>();
        if (p != null)
        {
            p.Init(dir, projectileSpeed);
            p.SetDamage(CalculateDamageFromDrive());
        }
    }

    private int CalculateDamageFromDrive()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning("Dynamo: PlayerStats instance not found. Using base damage.");
            return baseDamage;
        }

        float drive = PlayerStats.Instance.CurrentDrive;
        
        // Linear scaling: base + factor * drive
        // More exertion = more kinetic charge = more damage
        float scaled = baseDamage + drive * driveToDamageFactor;
        
        // Clamp to a reasonable range to avoid absurd values
        int finalDamage = Mathf.Clamp(Mathf.RoundToInt(scaled), 0, 100000);
        return finalDamage;
    }
}
