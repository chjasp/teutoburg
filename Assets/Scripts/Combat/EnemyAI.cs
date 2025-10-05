using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1.8f;

    [Header("Attack")]
    [SerializeField] private float attackInterval = 1.25f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 2.2f;

    [Header("Targeting")]
    [SerializeField] private Transform player; // auto-find by tag if not set
    [SerializeField] private string playerTag = "Player";

    [Header("Animation & Casting (optional)")]
    [SerializeField] private string castTriggerName = "CastSpell";
    [SerializeField] private string meleeTriggerName = "Melee";
    [SerializeField] private Animator animator;           // auto-find in children if not set
    [SerializeField] private SpellCaster spellCaster;     // auto-find on this object if not set

    private CharacterController controller;
    private float gravityVelocityY;
    private float attackTimer;
    private Transform lastAttackTarget;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGo != null) player = playerGo.transform;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (spellCaster == null)
        {
            spellCaster = GetComponent<SpellCaster>();
        }
    }

    void Update()
    {
        if (player == null) return;

        // Skip if target is dead
        var playerHealth = player.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        // Face the player
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(toPlayer.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.deltaTime);
        }

        // Move toward the player until within stopping distance
        Vector3 horizontalMove = Vector3.zero;
        if (distance > stoppingDistance)
        {
            horizontalMove = toPlayer.normalized * moveSpeed;
        }

        // Gravity for CharacterController
        gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(horizontalMove.x, gravityVelocityY, horizontalMove.z) * Time.deltaTime;
        var flags = controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
        {
            gravityVelocityY = -0.5f;
        }

        // Attack when in range and timer ready
        attackTimer += Time.deltaTime;
        if (distance <= attackRange && attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        if (player == null) return;

        // If we have a SpellCaster and an Animator, trigger the cast animation.
        if (spellCaster != null && animator != null && !string.IsNullOrEmpty(castTriggerName))
        {
            animator.SetTrigger(castTriggerName);
            // The actual projectile spawn is driven by the Animation Event calling SpellEventProxy.SpawnProjectile
            return;
        }

        // Melee path: trigger melee animation and defer damage to animation event
        if (animator != null && !string.IsNullOrEmpty(meleeTriggerName))
        {
            lastAttackTarget = player;
            animator.SetTrigger(meleeTriggerName);
            return;
        }

        // Fallback: apply direct damage without animation
        var damageable = player.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage);
        }
    }

    // Called by MeleeEventProxy from an Animation Event at the strike frame
    public void OnMeleeStrikeEvent()
    {
        var target = lastAttackTarget != null ? lastAttackTarget : (player != null ? player : null);
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > attackRange + 0.25f) return; // still close enough

        var damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage);
        }
    }
}


