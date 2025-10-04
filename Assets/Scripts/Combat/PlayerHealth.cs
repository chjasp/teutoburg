using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float hitInvulnerability = 0.25f; // short grace period between hits

    [Header("VFX (optional)")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private Animator animator;

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDied;

    private int currentHealth;
    private bool isDead;
    public bool IsDead => isDead;
    private float invulnerableUntil;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        currentHealth = Mathf.Max(1, maxHealth);
        invulnerableUntil = -999f;
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;
        if (Time.time < invulnerableUntil) return;

        int clamped = Mathf.Max(0, amount);
        if (clamped <= 0) return;

        currentHealth = Mathf.Clamp(currentHealth - clamped, 0, maxHealth);
        invulnerableUntil = Time.time + hitInvulnerability;

        if (hitVfxPrefab != null)
        {
            Instantiate(hitVfxPrefab, transform.position + Vector3.up * 1.2f, Quaternion.identity);
        }

        Debug.Log($"PlayerHealth: Took {clamped} damage. Now {currentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        int clamped = Mathf.Max(0, amount);
        if (clamped <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth + clamped, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }

        Debug.Log("PlayerHealth: Player died.");
        OnDied?.Invoke();

        // Optional: disable player control on death
        var mover = GetComponent<TopDownMover>();
        if (mover != null) mover.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
        {
            animator.SetTrigger(deathTriggerName);
            return;
        }

        FinalizeDeath();
    }

    public void FinalizeDeath()
    {
        if (!gameObject) return;
        // For player you might respawn; for now just keep object or later destroy.
        // Destroy(gameObject);
    }
}


