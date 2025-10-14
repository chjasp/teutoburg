using System;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class HealthBase : MonoBehaviour, IHealth, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("VFX/Animation (optional)")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private Animator animator;

    [Header("Death behavior")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelaySeconds = 0f;
    [SerializeField] private Behaviour[] componentsToDisableOnDeath;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    private int currentHealth;
    private bool isDead;
    private bool hasReportedDeath;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    protected virtual void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        currentHealth = Mathf.Max(1, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public virtual void TakeDamage(int amount)
    {
        if (!CanTakeDamageNow()) return;

        int clamped = Mathf.Max(0, amount);
        if (clamped <= 0) return;

        currentHealth = Mathf.Clamp(currentHealth - clamped, 0, maxHealth);
        OnAfterDamageTaken(clamped);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public virtual void Heal(int amount)
    {
        if (isDead) return;
        int clamped = Mathf.Max(0, amount);
        if (clamped <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth + clamped, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    protected virtual bool CanTakeDamageNow()
    {
        return !isDead;
    }

    protected virtual void OnAfterDamageTaken(int damageAmount)
    {
        // hook for subclasses (e.g., player hit vfx, invulnerability timer)
    }

    protected virtual void OnDeathStart()
    {
        // hook for subclasses (disable AI, movement, etc.)
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathVfxPrefab != null)
        {
            UnityEngine.Object.Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }

        OnDeathStart();

        if (componentsToDisableOnDeath != null)
        {
            for (int i = 0; i < componentsToDisableOnDeath.Length; i++)
            {
                if (componentsToDisableOnDeath[i] != null)
                {
                    componentsToDisableOnDeath[i].enabled = false;
                }
            }
        }

        ReportDeath();

        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
        {
            animator.SetTrigger(deathTriggerName);
            return;
        }

        FinalizeDeath();
    }

    private void ReportDeath()
    {
        if (hasReportedDeath) return;
        hasReportedDeath = true;
        OnDied?.Invoke();
    }

    public void FinalizeDeath()
    {
        OnDeathFinalize();
    }

    protected virtual void OnDeathFinalize()
    {
        if (!gameObject) return;
        if (destroyOnDeath)
        {
            if (destroyDelaySeconds > 0f)
            {
                Destroy(gameObject, destroyDelaySeconds);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
