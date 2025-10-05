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
    [SerializeField] private string allyTag = "Ally"; // Legionaries / allies to consider as targets
    [SerializeField] private float rescanInterval = 0.5f;

    [Header("Animation & Casting (optional)")]
    [SerializeField] private string castTriggerName = "CastSpell";
    [SerializeField] private string meleeTriggerName = "Melee";
    [SerializeField] private Animator animator;           // auto-find in children if not set
    [SerializeField] private SpellCaster spellCaster;     // auto-find on this object if not set

    private CharacterController controller;
    private float gravityVelocityY;
    private float attackTimer;
    private Transform lastAttackTarget;
    private Transform currentTarget;
    private float scanTimer;

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
        // Regularly reacquire best target between Player and Allies
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
        if (IsTargetDead(currentTarget))
        {
            currentTarget = null;
            return;
        }

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Face the target
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.deltaTime);
        }

        // Move toward the target until within stopping distance
        Vector3 horizontalMove = Vector3.zero;
        if (distance > stoppingDistance)
        {
            horizontalMove = toTarget.normalized * moveSpeed;
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
        if (currentTarget == null) return;

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
            lastAttackTarget = currentTarget;
            animator.SetTrigger(meleeTriggerName);
            return;
        }

        // Fallback: apply direct damage without animation
        var damageable = currentTarget.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage);
        }
    }

    // Called by MeleeEventProxy from an Animation Event at the strike frame
    public void OnMeleeStrikeEvent()
    {
        var target = lastAttackTarget != null ? lastAttackTarget : (currentTarget != null ? currentTarget : null);
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

    private void AcquireTarget()
    {
        float bestDist = float.MaxValue;
        Transform best = null;
        Vector3 myPos = transform.position;

        // Consider Player
        if (player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGo != null) player = playerGo.transform;
        }
        if (player != null && !IsTargetDead(player))
        {
            float d = Vector3.Distance(myPos, player.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = player;
            }
        }

        // Consider Allies (Legionaries)
        GameObject[] allies = null;
        try
        {
            allies = GameObject.FindGameObjectsWithTag(allyTag);
        }
        catch
        {
            allies = null;
        }

        if (allies != null)
        {
            foreach (var a in allies)
            {
                if (a == null) continue;
                var t = a.transform;
                if (IsTargetDead(t)) continue;
                float d = Vector3.Distance(myPos, t.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }
        }

        currentTarget = best;
    }

    private bool IsTargetDead(Transform t)
    {
        if (t == null) return true;
        var ph = t.GetComponentInParent<PlayerHealth>();
        if (ph != null) return ph.IsDead;
        var ah = t.GetComponentInParent<AllyHealth>();
        if (ah != null) return ah.IsDead;
        return false;
    }
}


