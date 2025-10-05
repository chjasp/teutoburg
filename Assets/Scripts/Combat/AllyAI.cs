using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class AllyAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float detectionRadius = 20f;
    [SerializeField] private float rescanInterval = 0.5f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float stoppingDistance = 1.6f;

    [Header("Attack")]
    [SerializeField] private float attackInterval = 0.9f;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackRange = 1.9f;
    [SerializeField] private string meleeTriggerName = "Melee";
    [SerializeField] private Animator animator; // auto-find in children if not set

    private CharacterController controller;
    private float gravityVelocityY;
    private float attackTimer;
    private float scanTimer;
    private Transform currentTarget;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Regularly reacquire target
        scanTimer += Time.deltaTime;
        if (currentTarget == null || scanTimer >= rescanInterval)
        {
            scanTimer = 0f;
            AcquireTarget();
        }

        if (currentTarget == null)
        {
            // Idle gravity only
            gravityVelocityY += Physics.gravity.y * Time.deltaTime;
            controller.Move(new Vector3(0f, gravityVelocityY, 0f) * Time.deltaTime);
            if ((controller.collisionFlags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
                gravityVelocityY = -0.5f;
            return;
        }

        // If target is dead, clear and rescan next frame
        var enemyHealth = currentTarget.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            currentTarget = null;
            return;
        }

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Face target
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.deltaTime);
        }

        // Move toward target until within stopping distance
        Vector3 horizontalMove = Vector3.zero;
        if (distance > stoppingDistance)
        {
            horizontalMove = toTarget.normalized * moveSpeed;
        }

        // Apply CharacterController motion with gravity
        gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(horizontalMove.x, gravityVelocityY, horizontalMove.z) * Time.deltaTime;
        var flags = controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
        {
            gravityVelocityY = -0.5f;
        }

        // Attack if in range and timer ready
        attackTimer += Time.deltaTime;
        if (distance <= attackRange && attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            PerformAttack();
        }
    }

    private void AcquireTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float bestDist = float.MaxValue;
        Transform best = null;
        Vector3 myPos = transform.position;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            var eh = e.GetComponentInParent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;
            float d = Vector3.Distance(myPos, e.transform.position);
            if (d <= detectionRadius && d < bestDist)
            {
                bestDist = d;
                best = e.transform;
            }
        }
        currentTarget = best;
    }

    private void PerformAttack()
    {
        if (currentTarget == null) return;

        // Prefer animation-driven melee
        if (animator != null && !string.IsNullOrEmpty(meleeTriggerName))
        {
            animator.SetTrigger(meleeTriggerName);
            return;
        }

        // Fallback direct damage
        var damageable = currentTarget.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage);
        }
        else
        {
            currentTarget = null;
        }
    }

    // Called by MeleeEventProxy from an Animation Event at the strike frame
    public void OnMeleeStrikeEvent()
    {
        if (currentTarget == null) return;
        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > attackRange + 0.25f) return;

        var damageable = currentTarget.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage);
        }
    }
}


