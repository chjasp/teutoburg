using Teutoburg.Combat;
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
        scanTimer += Time.deltaTime;
        if (currentTarget == null || scanTimer >= rescanInterval)
        {
            scanTimer = 0f;
            AcquireTarget();
        }

        if (currentTarget == null)
        {
            gravityVelocityY += Physics.gravity.y * Time.deltaTime;
            controller.Move(new Vector3(0f, gravityVelocityY, 0f) * Time.deltaTime);
            if ((controller.collisionFlags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
                gravityVelocityY = -0.5f;
            return;
        }

        var enemyHealth = currentTarget.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            currentTarget = null;
            return;
        }

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.deltaTime);
        }

        Vector3 horizontalMove = Vector3.zero;
        if (distance > stoppingDistance)
        {
            horizontalMove = toTarget.normalized * moveSpeed;
        }

        gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(horizontalMove.x, gravityVelocityY, horizontalMove.z) * Time.deltaTime;
        var flags = controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
        {
            gravityVelocityY = -0.5f;
        }

        attackTimer += Time.deltaTime;
        if (distance <= attackRange && attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            PerformAttack();
        }
    }

    private void AcquireTarget()
    {
        float distance;
        currentTarget = CombatTargetingUtility.FindClosestTarget(enemyTag, transform.position, detectionRadius, IsEnemyDead, out distance);
    }

    private bool IsEnemyDead(Transform candidate)
    {
        var eh = candidate.GetComponentInParent<EnemyHealth>();
        return eh != null && eh.IsDead;
    }

    private void PerformAttack()
    {
        if (currentTarget == null) return;

        if (animator != null && !string.IsNullOrEmpty(meleeTriggerName))
        {
            animator.SetTrigger(meleeTriggerName);
            return;
        }

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
        damageable?.TakeDamage(attackDamage);
    }
}
