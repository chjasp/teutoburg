using Teutoburg.Combat;
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
    [SerializeField] private string meleeTriggerName = "Melee";

    [Header("Targeting")]
    [SerializeField] private Transform player; // auto-find by tag if not set
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string allyTag = "Ally"; // Legionaries / allies to consider as targets
    [SerializeField] private float rescanInterval = 0.5f;

    [Header("Animation & Casting (optional)")]
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
        EnsurePlayerReference();

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

        if (IsTargetDead(currentTarget))
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

    private void PerformAttack()
    {
        if (currentTarget == null) return;

        if (spellCaster != null)
        {
            spellCaster.RequestCast();
            return;
        }

        if (animator != null && !string.IsNullOrEmpty(meleeTriggerName))
        {
            lastAttackTarget = currentTarget;
            animator.SetTrigger(meleeTriggerName);
            return;
        }

        var damageable = currentTarget.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(attackDamage);
    }

    // Called by MeleeEventProxy from an Animation Event at the strike frame
    public void OnMeleeStrikeEvent()
    {
        var target = lastAttackTarget != null ? lastAttackTarget : currentTarget;
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > attackRange + 0.25f) return;

        var damageable = target.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(attackDamage);
    }

    private void AcquireTarget()
    {
        Vector3 myPos = transform.position;
        float bestDist = float.MaxValue;
        Transform best = null;

        EnsurePlayerReference();
        if (player != null && !IsTargetDead(player))
        {
            float d = Vector3.Distance(myPos, player.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = player;
            }
        }

        float allyDist;
        var ally = CombatTargetingUtility.FindClosestTarget(allyTag, myPos, Mathf.Infinity, IsTargetDead, out allyDist);
        if (ally != null && allyDist < bestDist)
        {
            bestDist = allyDist;
            best = ally;
        }

        currentTarget = best;
    }

    private void EnsurePlayerReference()
    {
        if (player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGo != null) player = playerGo.transform;
        }
    }

    private bool IsTargetDead(Transform t)
    {
        if (t == null) return true;
        var ph = t.GetComponentInParent<PlayerHealth>();
        if (ph != null) return ph.IsDead;
        var ah = t.GetComponentInParent<AllyHealth>();
        if (ah != null) return ah.IsDead;
        var eh = t.GetComponentInParent<EnemyHealth>();
        if (eh != null) return eh.IsDead;
        return false;
    }
}
