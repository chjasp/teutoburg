using Teutoburg.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMelee : AnimationDrivenAbility
{
    [Header("Targeting")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float maxStrikeDistance = 2.2f;
    [SerializeField] private float autoAimAngle = 45f; // degrees cone in front

    [Header("Damage")]
    [SerializeField] private int damage = 25;

    private Transform lastTarget;

    protected override void Awake()
    {
        base.Awake();
        EnsureTriggerName("Melee");
    }

    protected override void Prepare()
    {
        lastTarget = FindBestTarget();
    }

    protected override void Execute()
    {
        Transform target = lastTarget != null ? lastTarget : FindBestTarget();
        lastTarget = null;

        if (target == null)
        {
            return;
        }

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        if (to.magnitude > maxStrikeDistance + 0.25f)
        {
            return;
        }

        var damageable = target.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(damage);
    }

    public void TriggerMelee()
    {
        Perform();
    }

    // Called by MeleeEventProxy via Animation Event at strike frame
    public void OnMeleeStrikeEvent()
    {
        ExecuteFromAnimationEvent();
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
            if (e == null)
            {
                continue;
            }

            var eh = e.GetComponentInParent<EnemyHealth>();
            if (eh != null && eh.IsDead)
            {
                continue;
            }

            Vector3 to = e.transform.position - origin;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > maxStrikeDistance)
            {
                continue;
            }

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
}
