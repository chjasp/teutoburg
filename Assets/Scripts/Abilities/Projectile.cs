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

    // Called by Dynamo right after Instantiate
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

        Debug.Log($"[Projectile] Hit: {other.name} (tag: {other.tag}, type: {other.GetType().Name})");

        // Existing: explode + show damage on enemies
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
