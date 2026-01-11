using System;
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Loot
{
    /// <summary>
    /// Author-time definition of an item. Holds base stats, visuals, rarity and possible affixes.
    /// </summary>
    [CreateAssetMenu(menuName = "Axiom/Loot/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity & Presentation")]
        [SerializeField] private string id = Guid.Empty.ToString();
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("Classification")]
        [SerializeField] private ItemType itemType = ItemType.Weapon;
        [SerializeField] private EquipmentSlot allowedEquipmentSlot = EquipmentSlot.MainHand;
        [SerializeField] private Rarity rarity = Rarity.Common;
        [Tooltip("Relative drop chance contribution of this item definition.")]
        [SerializeField] private int rarityWeight = 1;

        [Header("Base Stats")]
        [Tooltip("For Weapon-like items only.")]
        [SerializeField] private int baseMinDamage = 1;
        [Tooltip("For Weapon-like items only.")]
        [SerializeField] private int baseMaxDamage = 3;
        [Tooltip("For Armor-like items only.")]
        [SerializeField] private int baseArmor = 0;

        [Header("Affixes")]
        [Tooltip("Candidate affixes that may roll on this item when generated.")]
        [SerializeField] private List<AffixDefinition> possibleAffixes = new List<AffixDefinition>();

        #region Public API

        /// <summary>
        /// Unique identifier for this item definition. Keep stable across builds.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Display name used in UI.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Description used for tooltips.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Sprite icon used in UI.
        /// </summary>
        public Sprite Icon => icon;

        /// <summary>
        /// High-level type of item (drives base stat semantics).
        /// </summary>
        public ItemType ItemType => itemType;

        /// <summary>
        /// Which equipment slot this item can be equipped into.
        /// </summary>
        public EquipmentSlot AllowedEquipmentSlot => allowedEquipmentSlot;

        /// <summary>
        /// Rarity tier of the item.
        /// </summary>
        public Rarity Rarity => rarity;

        /// <summary>
        /// Weight used when this item is in a loot table selection.
        /// </summary>
        public int RarityWeight => Mathf.Max(1, rarityWeight);

        /// <summary>
        /// Base minimum damage (for Weapon types).
        /// </summary>
        public int BaseMinDamage => Mathf.Max(0, baseMinDamage);

        /// <summary>
        /// Base maximum damage (for Weapon types).
        /// </summary>
        public int BaseMaxDamage => Mathf.Max(BaseMinDamage, baseMaxDamage);

        /// <summary>
        /// Base armor value (for Armor types).
        /// </summary>
        public int BaseArmor => Mathf.Max(0, baseArmor);

        /// <summary>
        /// Returns the list of affix definitions that are candidates for rolling on this item.
        /// </summary>
        public IReadOnlyList<AffixDefinition> PossibleAffixes => possibleAffixes;

        #endregion
    }
}


