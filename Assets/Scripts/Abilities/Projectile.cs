// Projectile.cs
using UnityEngine;

public class Projectile : MonoBehaviour
{
    // Movement
    private Vector3 direction = Vector3.forward;
    private float speed = 10f;

    // Explosion / lifetime
    [Header("Explosion")]
    [SerializeField] private float lifetime = 3f;               // explode after this many seconds
    [SerializeField] private GameObject explosionPrefab;         // your particle burst
    [SerializeField] private string explodeOnTag = "Enemy";      // what we react to

    // Damage text
    [Header("Damage")]
    [SerializeField] private int damage = 50;                    // shown number
    [SerializeField] private DamageText damageTextPrefab;        // assign the DamageText prefab

    private float lifeTimer;
    private bool hasExploded;

    // Called by SpellCaster right after Instantiate
    public void Init(Vector3 dir, float spd)
    {
        direction = dir.normalized;
        speed = spd;
        transform.forward = direction; // orient the visual
    }

    public void SetDamage(int value)
    {
        damage = Mathf.Max(0, value);
    }

    void Update()
    {
        // Move forward
        transform.position += direction * speed * Time.deltaTime;

        // Timeout explosion
        lifeTimer += Time.deltaTime;
        if (!hasExploded && lifeTimer >= lifetime)
        {
            Explode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;

        // Existing: explode + show damage on enemies
        if (other.CompareTag(explodeOnTag))
        {
            ApplyDamage(other);
            ShowDamage(other);
            Explode();
            return;
        }

        // NEW: explode when touching Unity Terrain
        if (other is TerrainCollider)
        {
            Explode();
            return;
        }
    }


    private void ApplyDamage(Collider enemy)
    {
        var damageable = enemy.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    private void ShowDamage(Collider enemy)
    {
        if (damageTextPrefab == null) return;

        // Place text above the enemy's collider
        var top = enemy.bounds.center + Vector3.up * (enemy.bounds.extents.y + 0.5f);

        // Parent to enemy so it follows if the enemy moves
        var dt = Instantiate(damageTextPrefab, top, Quaternion.identity, enemy.transform);
        dt.Init(damage);
    }

    private void Explode()
    {
        hasExploded = true;

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
