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
    [SerializeField] private bool explodeOnEnvironment = true;   // explode on walls/obstacles
    [SerializeField] private LayerMask ignoreLayers;             // layers to pass through (e.g., Player)
    [SerializeField] private float spawnImmunityTime = 0.05f;    // ignore collisions for this long after spawn

    // Damage text
    [Header("Damage")]
    [SerializeField] private int damage = 50;                    // shown number
    [SerializeField] private DamageText damageTextPrefab;        // assign the DamageText prefab

    private float lifeTimer;
    private bool hasExploded;

    void Start()
    {
        Debug.Log($"[Projectile] Start() called at {transform.position}");
    }

    void OnEnable()
    {
        Debug.Log($"[Projectile] OnEnable() called");
    }

    void OnDestroy()
    {
        Debug.Log($"[Projectile] OnDestroy() called - hasExploded: {hasExploded}, lifeTimer: {lifeTimer:F2}s");
    }

    // Called by Heartfire right after Instantiate
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
        if (hasExploded) return;
        
        // Move forward
        transform.position += direction * speed * Time.deltaTime;

        // Timeout explosion
        lifeTimer += Time.deltaTime;
        
        // Log first few frames to verify movement
        if (lifeTimer < 0.1f)
        {
            Debug.Log($"[Projectile] Update - pos: {transform.position}, lifeTimer: {lifeTimer:F3}s");
        }
        
        if (lifeTimer >= lifetime)
        {
            Debug.Log($"[Projectile] Lifetime expired ({lifetime}s) - exploding");
            Explode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        
        // Brief immunity after spawn to avoid hitting the player who fired
        if (lifeTimer < spawnImmunityTime) return;

        // Check if this layer should be ignored (e.g., Player layer)
        int otherLayer = other.gameObject.layer;
        if (((1 << otherLayer) & ignoreLayers) != 0)
        {
            return; // Pass through ignored layers
        }

        Debug.Log($"[Projectile] Hit: {other.name} (tag: {other.tag}, layer: {LayerMask.LayerToName(otherLayer)})");

        // Explode + apply damage on enemies
        if (other.CompareTag(explodeOnTag))
        {
            ApplyDamage(other);
            ShowDamage(other);
            Explode();
            return;
        }

        // Explode when touching Unity Terrain
        if (other is TerrainCollider)
        {
            Debug.Log("[Projectile] Hit terrain - exploding");
            Explode();
            return;
        }

        // Explode on any other solid environment (walls, obstacles, etc.)
        if (explodeOnEnvironment)
        {
            Debug.Log($"[Projectile] Hit environment ({other.name}) - exploding");
            Explode();
            return;
        }
    }

    // For walls/obstacles with non-trigger colliders
    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        
        // Brief immunity after spawn to avoid hitting the player who fired
        if (lifeTimer < spawnImmunityTime) return;

        int otherLayer = collision.gameObject.layer;
        if (((1 << otherLayer) & ignoreLayers) != 0)
        {
            return;
        }

        Debug.Log($"[Projectile] Collision with: {collision.gameObject.name} (layer: {LayerMask.LayerToName(otherLayer)})");

        // Check if it's an enemy
        if (collision.gameObject.CompareTag(explodeOnTag))
        {
            var damageable = collision.gameObject.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
            ShowDamageAtPoint(collision.contacts[0].point);
            Explode();
            return;
        }

        // Explode on environment collision
        if (explodeOnEnvironment)
        {
            Debug.Log($"[Projectile] Hit solid environment ({collision.gameObject.name}) - exploding");
            Explode();
        }
    }

    private void ShowDamageAtPoint(Vector3 point)
    {
        if (damageTextPrefab == null) return;
        var dt = Instantiate(damageTextPrefab, point + Vector3.up * 0.5f, Quaternion.identity);
        dt.Init(damage);
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
        Debug.Log($"[Projectile] Explode() called at {transform.position}");
        hasExploded = true;

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
