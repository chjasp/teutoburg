using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private Animator animator;

    private int currentHealth;
    private bool isDead;
    public bool IsDead => isDead;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        currentHealth = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, amount));
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }

        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
        {
            animator.SetTrigger(deathTriggerName);
            return;
        }

        Destroy(gameObject);
    }

    public void FinalizeDeath()
    {
        if (!gameObject) return;
        Destroy(gameObject);
    }
}


