using System;
using System.Collections.Generic;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// Runtime representation of an item created from an <see cref="ItemDefinition"/>.
    /// Holds rolled level and affixes, and exposes computed stats APIs.
    /// </summary>
    [Serializable]
    public sealed class ItemInstance
    {
        [Serializable]
        public sealed class AffixRoll
        {
            [SerializeField] private string affixId;
            [SerializeField] private AffixDefinition definition;
            [SerializeField] private float value;

            /// <summary>
            /// Definition used to compute the stat.
            /// </summary>
            public AffixDefinition Definition => definition;

            /// <summary>
            /// Rolled numeric value for this affix.
            /// </summary>
            public float Value => value;

            internal AffixRoll(AffixDefinition def, float rolled)
            {
                definition = def;
                affixId = def != null ? def.Id : string.Empty;
                value = rolled;
            }
        }

        [SerializeField] private ItemDefinition definition;
        [SerializeField] private int level;
        [SerializeField] private List<AffixRoll> affixes = new List<AffixRoll>();

        /// <summary>
        /// The source author-time definition of this item.
        /// </summary>
        public ItemDefinition Definition => definition;

        /// <summary>
        /// The level or power rating used when this item was rolled.
        /// </summary>
        public int Level => level;

        /// <summary>
        /// The list of affix rolls this item has.
        /// </summary>
        public IReadOnlyList<AffixRoll> Affixes => affixes;

        /// <summary>
        /// Creates a new <see cref="ItemInstance"/>.
        /// </summary>
        public ItemInstance(ItemDefinition definition, int level, List<AffixRoll> rolledAffixes)
        {
            this.definition = definition;
            this.level = Mathf.Max(1, level);
            if (rolledAffixes != null && rolledAffixes.Count > 0)
            {
                this.affixes = rolledAffixes;
            }
        }

        #region Computed Stats

        /// <summary>
        /// Computes this item's damage range, considering base stats and applicable affixes.
        /// Non-weapon items will return (0, 0).
        /// </summary>
        public void GetTotalDamageRange(out int minDamage, out int maxDamage)
        {
            if (definition == null || definition.ItemType != ItemType.Weapon)
            {
                minDamage = 0;
                maxDamage = 0;
                return;
            }

            float baseMin = definition.BaseMinDamage;
            float baseMax = definition.BaseMaxDamage;
            float flatAdd = 0f;
            float percent = 0f;

            for (int i = 0; i < affixes.Count; i++)
            {
                var roll = affixes[i];
                if (roll == null || roll.Definition == null) continue;
                switch (roll.Definition.Type)
                {
                    case AffixType.FlatDamage:
                        flatAdd += roll.Value;
                        break;
                    case AffixType.PercentDamage:
                        // percent stored as 10 for 10%
                        percent += roll.Value * 0.01f;
                        break;
                }
            }

            float min = baseMin;
            float max = baseMax;
            if (percent != 0f)
            {
                min += baseMin * percent;
                max += baseMax * percent;
            }
            if (flatAdd != 0f)
            {
                min += flatAdd;
                max += flatAdd;
            }

            minDamage = Mathf.Max(0, Mathf.RoundToInt(min));
            maxDamage = Mathf.Max(minDamage, Mathf.RoundToInt(max));
        }

        /// <summary>
        /// Computes the armor bonus for this item (base armor plus affixes if applicable).
        /// Non-armor items will return 0.
        /// </summary>
        public int GetArmorBonus()
        {
            if (definition == null || definition.ItemType != ItemType.Armor)
            {
                return 0;
            }

            float baseArmor = definition.BaseArmor;
            float flatAdd = 0f;
            float percent = 0f;

            for (int i = 0; i < affixes.Count; i++)
            {
                var roll = affixes[i];
                if (roll == null || roll.Definition == null) continue;
                switch (roll.Definition.Type)
                {
                    case AffixType.FlatArmor:
                        flatAdd += roll.Value;
                        break;
                    case AffixType.PercentArmor:
                        percent += roll.Value * 0.01f;
                        break;
                }
            }

            float total = baseArmor;
            if (percent != 0f)
            {
                total += baseArmor * percent;
            }
            total += flatAdd;
            return Mathf.Max(0, Mathf.RoundToInt(total));
        }

        #endregion
    }
}


