// Heartfire.cs
using System.Collections;
using UnityEngine;
using Axiom.Core;

/// <summary>
/// Heartfire - Channels primal flame energy through the body.
/// The spell draws power from your inner fire, fueled by physical exertion.
/// More movement means more heat, more damage.
/// Scales with: Drive
/// </summary>
public class Heartfire : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform handSocket;   // where the shot spawns

    [Header("Animation")]
    [SerializeField] private Animator animator;              // auto-find in children if not set
    [SerializeField] private string castTriggerName = "CastSpell";
    [SerializeField] private float projectileDelay = 0.4f;   // delay to sync with animation

    [Header("Tuning")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float spawnForwardOffset = 0.12f;
    [SerializeField] private int baseDamage = 50;
    [SerializeField] private float driveToDamageFactor = 2.5f;

    [Header("Aim Assistance")]
    [SerializeField, Range(0f, 1f)] private float aimAssistStrength = 0.6f;
    [SerializeField] private float aimAssistRange = 15f;
    [SerializeField] private float aimAssistAngle = 45f;
    [SerializeField] private string enemyTag = "Enemy";

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    /// <summary>
    /// UI Button entry point - plays animation and spawns projectile after delay.
    /// </summary>
    public void Cast()
    {
        if (animator != null)
        {
            animator.SetTrigger(castTriggerName);
        }

        StartCoroutine(SpawnAfterDelay());
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(projectileDelay);
        SpawnProjectile();
    }

    private void SpawnProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Heartfire: No projectilePrefab assigned.");
            return;
        }

        Vector3 rawDir = transform.forward;
        Vector3 aimDir = GetAimAssistedDirection(rawDir);
        
        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.LookRotation(aimDir);

        if (handSocket != null)
        {
            spawnPos = handSocket.position + handSocket.forward * spawnForwardOffset;
        }
        else
        {
            spawnPos = transform.position + Vector3.up * 1.4f + rawDir * 0.5f;
        }

        var go = Instantiate(projectilePrefab, spawnPos, spawnRot);
        var p = go.GetComponent<Projectile>();
        if (p != null)
        {
            p.Init(aimDir, projectileSpeed);
            p.SetDamage(CalculateDamageFromDrive());
        }
    }

    /// <summary>
    /// Finds the best enemy target and blends aim direction toward it based on aimAssistStrength.
    /// </summary>
    private Vector3 GetAimAssistedDirection(Vector3 rawDirection)
    {
        if (aimAssistStrength <= 0f) return rawDirection;

        Transform bestTarget = FindBestTarget();
        if (bestTarget == null) return rawDirection;

        // Calculate direction to target (aim at center mass, slightly elevated)
        Vector3 targetPos = bestTarget.position + Vector3.up * 1f;
        Vector3 toTarget = (targetPos - (handSocket != null ? handSocket.position : transform.position)).normalized;

        // Blend between raw direction and target direction based on assist strength
        Vector3 assistedDir = Vector3.Slerp(rawDirection, toTarget, aimAssistStrength);
        return assistedDir.normalized;
    }

    /// <summary>
    /// Finds the best enemy within range and angle cone.
    /// </summary>
    private Transform FindBestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        Transform best = null;
        float bestScore = float.MaxValue;
        
        Vector3 origin = handSocket != null ? handSocket.position : transform.position + Vector3.up * 1.4f;
        Vector3 fwd = transform.forward;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            // Skip dead enemies
            var eh = enemy.GetComponentInParent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;

            Vector3 toEnemy = enemy.transform.position - origin;
            float dist = toEnemy.magnitude;

            // Check range
            if (dist > aimAssistRange) continue;

            // Check angle cone
            float angle = Vector3.Angle(fwd, toEnemy);
            if (angle > aimAssistAngle) continue;

            // Score: prefer enemies closer to crosshair (lower angle) and closer distance
            // Weight angle more heavily to prioritize what you're aiming at
            float score = angle * 2f + dist;
            if (score < bestScore)
            {
                bestScore = score;
                best = enemy.transform;
            }
        }

        return best;
    }

    private int CalculateDamageFromDrive()
    {
        if (PlayerStats.Instance == null)
        {
            return baseDamage;
        }

        float drive = PlayerStats.Instance.CurrentDrive;
        float scaled = baseDamage + drive * driveToDamageFactor;
        return Mathf.Clamp(Mathf.RoundToInt(scaled), 0, 100000);
    }
}
