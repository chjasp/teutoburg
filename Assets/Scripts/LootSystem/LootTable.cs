using System;
using System.Collections.Generic;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// ScriptableObject describing the items an enemy type can drop, with weights.
    /// </summary>
    [CreateAssetMenu(menuName = "Teutoburg/Loot/Loot Table", fileName = "LootTable")]
    public sealed class LootTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField] private int weight = 1;

            /// <summary>
            /// Item that can drop from this table.
            /// </summary>
            public ItemDefinition Item => item;

            /// <summary>
            /// Relative weight used for selection.
            /// </summary>
            public int Weight => Mathf.Max(1, weight);
        }

        [Header("Identity")]
        [SerializeField] private string id = Guid.Empty.ToString();
        [SerializeField] private string displayName;

        [Header("Drop Count")]
        [Tooltip("Minimum number of items to generate.")]
        [SerializeField] private int minItems = 0;
        [Tooltip("Maximum number of items to generate.")]
        [SerializeField] private int maxItems = 3;

        [Header("Entries")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>
        /// Unique identifier for this loot table.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Designer-facing name.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Minimum number of items to roll.
        /// </summary>
        public int MinItems => Mathf.Max(0, minItems);

        /// <summary>
        /// Maximum number of items to roll.
        /// </summary>
        public int MaxItems => Mathf.Max(MinItems, maxItems);

        /// <summary>
        /// The list of weighted item entries.
        /// </summary>
        public IReadOnlyList<Entry> Entries => entries;
    }
}


