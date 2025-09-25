using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private float attackRange = 2f;        // desired edge-to-edge range
    [SerializeField] private float arrivalTolerance = 0.1f; // small buffer
    [SerializeField] private float damage = 25f;            // damage dealt per attack
    private NavMeshAgent agent;
    private Transform target;
    private Animator animator;
    [SerializeField] private string attackTrigger = "Attack";

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (target == null) return;

        // Desired center-to-center stop distance = attack range + both radii
        float stopDist = attackRange + GetRadius(target) + agent.radius;

        // Compute a point on a ring around the target and snap it to the NavMesh
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 desired = target.position - toTarget.normalized * stopDist;
        if (NavMesh.SamplePosition(desired, out var hit, 1.5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);

        // We now stop exactly at the ring, so keep the agent's own stoppingDistance tiny
        agent.stoppingDistance = 0.05f;

        // Use planar (XZ) distance to decide when to attack
        float planarDist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(target.position.x,    target.position.z));

        if (planarDist <= stopDist + arrivalTolerance)
        {
            agent.ResetPath();
            FaceTarget();
            Attack();
            target = null;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target == null)
        {
            agent.ResetPath();
            agent.stoppingDistance = 0f; // restore default for free move clicks
        }
    }

    private float GetRadius(Transform t)
    {
        if (t.TryGetComponent<CapsuleCollider>(out var cap))  return cap.radius;
        if (t.TryGetComponent<CharacterController>(out var cc)) return cc.radius;
        if (t.TryGetComponent<SphereCollider>(out var sph))   return sph.radius;
        // Fallback: half the XZ size of the renderer bounds
        var r = t.GetComponentInChildren<Renderer>();
        return r ? Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) : 0f;
    }

    private void Attack()
    {
        if (animator != null)
            animator.SetTrigger(attackTrigger);

        if (target != null)
        {
            Health health = target.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Debug.Log($"Attacking {target.name} for {damage} damage");
            }
        }
    }

    private void FaceTarget()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
