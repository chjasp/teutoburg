using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Teutoburg.Combat
{
    /// <summary>
    /// Shared health implementation for non-player combatants.
    /// Handles damage processing, death effects and animator triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NpcHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;

        [Header("Death Effects")]
        [SerializeField] private GameObject deathVfxPrefab;

        [FormerlySerializedAs("deathTriggerName")]
        [SerializeField] private string deathTrigger = "Death";

        [SerializeField] private Animator animator;

        private int currentHealth;
        private bool isDead;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;

        protected virtual void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            currentHealth = Mathf.Max(1, maxHealth);
            NotifyHealthChanged();
        }

        protected virtual void Reset()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        public void TakeDamage(int amount)
        {
            if (isDead)
            {
                return;
            }

            int clamped = Mathf.Max(0, amount);
            if (clamped <= 0)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth - clamped, 0, maxHealth);
            NotifyHealthChanged();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        protected void Heal(int amount)
        {
            if (isDead)
            {
                return;
            }

            int clamped = Mathf.Max(0, amount);
            if (clamped <= 0)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + clamped, 0, maxHealth);
            NotifyHealthChanged();
        }

        protected void NotifyHealthChanged()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        protected virtual void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;

            if (deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            OnBeforeDeath();
            OnDied?.Invoke();

            if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            {
                animator.SetTrigger(deathTrigger);
            }
            else
            {
                FinalizeDeath();
            }
        }

        /// <summary>
        /// Override to disable AI, physics etc when death occurs.
        /// </summary>
        protected abstract void OnBeforeDeath();

        /// <summary>
        /// Called from an animation event to clean up the GameObject. May also be invoked directly.
        /// </summary>
        public virtual void FinalizeDeath()
        {
            if (!gameObject)
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
