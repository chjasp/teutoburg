using System.Collections.Generic;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// Listens for enemy death and spawns world pickups for generated loot.
    /// Attach to enemy prefab alongside EnemyLoot and Health component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyLoot))]
    public sealed class LootDropper : MonoBehaviour
    {
        [Header("Pickup Spawning")]
        [SerializeField] private WorldItemPickup pickupPrefab;
        [Tooltip("Scatter radius for spawned pickups around the enemy position.")]
        [SerializeField] private float scatterRadius = 1.2f;
        [Tooltip("Y offset for spawned pickups, to avoid ground clipping.")]
        [SerializeField] private float spawnYOffset = 0.15f;

        [Header("Enemy Power")]
        [Tooltip("Enemy level used for item rolling. Replace with your leveling system when available.")]
        [SerializeField] private int enemyLevel = 1;

        private EnemyLoot enemyLoot;
        private HealthBase health;

        void Awake()
        {
            enemyLoot = GetComponent<EnemyLoot>();
            health = GetComponent<HealthBase>();
            if (health != null)
            {
                health.OnDied += OnEnemyDied;
            }
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.OnDied -= OnEnemyDied;
            }
        }

        private void OnEnemyDied()
        {
            if (enemyLoot == null || pickupPrefab == null) return;
            List<ItemInstance> items = enemyLoot.GenerateLootForEnemy(Mathf.Max(1, enemyLevel));
            if (items == null || items.Count == 0) return;
            Vector3 origin = transform.position + Vector3.up * spawnYOffset;
            for (int i = 0; i < items.Count; i++)
            {
                Vector2 ring = Random.insideUnitCircle * scatterRadius;
                Vector3 pos = origin + new Vector3(ring.x, 0f, ring.y);
                var pickup = Instantiate(pickupPrefab, pos, Quaternion.identity);
                pickup.Initialize(items[i]);
            }
        }
    }
}


