using UnityEngine;

// Health component for managing object health and damage
[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = false;

    private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    void Awake()
    {
        currentHealth = Mathf.Clamp(maxHealth, 0f, float.MaxValue);
    }

    public void TakeDamage(float damageAmount)
    {
        if (IsDead) return;
        if (damageAmount <= 0f) return;

        currentHealth = Mathf.Max(currentHealth - damageAmount, 0f);

        if (currentHealth <= 0f)
        {
            Debug.Log($"{gameObject.name} has died.  {currentHealth} health remaining.");
            Die();
        }
    }

    public void Heal(float healAmount)
    {
        if (healAmount <= 0f) return;
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
    }

    private void Die()
    {
        if (destroyOnDeath)
        {
            Destroy(gameObject);
            return;
        }

        enabled = false;

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
        }

        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
    }
}


