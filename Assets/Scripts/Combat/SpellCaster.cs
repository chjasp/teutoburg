// SpellCaster.cs
using UnityEngine;

public class SpellCaster : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform handSocket;   // where the orb spawns

    [Header("Tuning")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float spawnForwardOffset = 0.12f; // in front of the hand

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
        }
    }
}
