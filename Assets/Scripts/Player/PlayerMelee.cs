using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMelee : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float maxStrikeDistance = 2.2f;
    [SerializeField] private float autoAimAngle = 45f; // degrees cone in front

    [Header("Damage")]
    [SerializeField] private int damage = 25; // fallback/base damage
    [SerializeField] private DamageText damageTextPrefab;

    [Header("Animation")]
    [SerializeField] private string meleeTriggerName = "Melee";
    [SerializeField] private Animator animator; // auto-find in children

    private Transform lastTarget;

    public int BaseDamage => damage;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public void TriggerMelee()
    {
        // pick a target now to reduce pop during windup; still re-check at strike
        lastTarget = FindBestTarget();
        if (animator != null && !string.IsNullOrEmpty(meleeTriggerName))
        {
            animator.SetTrigger(meleeTriggerName);
        }
        else
        {
            // fallback: apply instantly
            ApplyStrike();
        }
    }

    private Transform FindBestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        Transform best = null;
        float bestAngle = autoAimAngle;
        float bestDist = maxStrikeDistance;
        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            var eh = e.GetComponentInParent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;
            Vector3 to = e.transform.position - origin;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > maxStrikeDistance) continue;
            float angle = Vector3.Angle(fwd, to);
            if (angle <= bestAngle && dist <= bestDist)
            {
                best = e.transform;
                bestAngle = angle;
                bestDist = dist;
            }
        }
        return best;
    }

    // Called by MeleeEventProxy via Animation Event at strike frame
    public void OnMeleeStrikeEvent()
    {
        ApplyStrike();
    }

    private void ApplyStrike()
    {
        Transform target = lastTarget != null ? lastTarget : FindBestTarget();
        if (target == null) return;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        if (to.magnitude > maxStrikeDistance + 0.25f) return;

        var damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            ShowDamageText(target, damage);
        }
    }

    private void ShowDamageText(Transform target, int amount)
    {
        if (damageTextPrefab == null || target == null) return;

        Collider col = target.GetComponentInChildren<Collider>();
        if (col == null) col = target.GetComponent<Collider>();

        Vector3 spawnPos;
        Transform parent;
        if (col != null)
        {
            spawnPos = col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.5f);
            parent = col.transform;
        }
        else
        {
            spawnPos = target.position + Vector3.up * 1.2f;
            parent = target;
        }

        var dt = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity, parent);
        dt.Init(amount);
    }
}
