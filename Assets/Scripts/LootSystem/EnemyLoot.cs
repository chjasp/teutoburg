using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Loot
{
    /// <summary>
    /// Minimal enemy-side component holding a reference to a loot table and exposing loot generation API.
    /// Does not spawn world drops; it only returns the generated items.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyLoot : MonoBehaviour
    {
        [Header("Loot")]
        [SerializeField] private LootTable lootTable;
        [Tooltip("Optional extra bias towards higher rarity when generating loot.")]
        [Range(0f, 2f)]
        [SerializeField] private float rarityModifier = 0f;

        /// <summary>
        /// Loot table assigned to this enemy.
        /// </summary>
        public LootTable LootTable
        {
            get => lootTable;
            set => lootTable = value;
        }

        /// <summary>
        /// Generates loot for this enemy instance.
        /// </summary>
        /// <param name="enemyLevel">Enemy level driving item power.</param>
        public List<ItemInstance> GenerateLootForEnemy(int enemyLevel)
        {
            return LootGenerator.GenerateLoot(enemyLevel, lootTable, rarityModifier);
        }
    }
}


