using System;
using System.Collections.Generic;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// Static utility that generates loot items from a <see cref="LootTable"/>.
    /// </summary>
    public static class LootGenerator
    {
        /// <summary>
        /// Generate item instances from a loot table for an enemy level.
        /// </summary>
        /// <param name="enemyLevel">Enemy level influencing stat rolls.</param>
        /// <param name="lootTable">Loot table to use.</param>
        /// <param name="rarityModifier">
        /// Optional bias favoring higher rarity: 0 = neutral, 0.5 moderately favors higher rarities.
        /// </param>
        /// <returns>List of generated item instances (may be empty).</returns>
        public static List<ItemInstance> GenerateLoot(int enemyLevel, LootTable lootTable, float rarityModifier = 0f)
        {
            var results = new List<ItemInstance>();
            if (lootTable == null) return results;
            var entries = lootTable.Entries;
            if (entries == null || entries.Count == 0) return results;

            int count = UnityEngine.Random.Range(lootTable.MinItems, lootTable.MaxItems + 1);
            for (int i = 0; i < count; i++)
            {
                var chosen = SelectWeighted(entries, rarityModifier);
                if (chosen == null || chosen.Item == null) continue;
                var instance = RollItemInstance(enemyLevel, chosen.Item);
                if (instance != null)
                {
                    results.Add(instance);
                }
            }
            return results;
        }

        private static LootTable.Entry SelectWeighted(IReadOnlyList<LootTable.Entry> entries, float rarityModifier)
        {
            // Compute total weight with rarity bias
            long total = 0;
            var weights = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.Item == null) { weights[i] = 0; continue; }
                int baseWeight = e.Weight;
                int itemWeight = e.Item.RarityWeight;
                float rarityBias = 1f + Mathf.Max(0f, rarityModifier) * (int)e.Item.Rarity;
                long w = Mathf.Max(0, Mathf.RoundToInt(baseWeight * itemWeight * rarityBias));
                if (w <= 0) { weights[i] = 0; continue; }
                // Clamp to int range to remain compatible with Random.Range
                int clamped = w > int.MaxValue ? int.MaxValue : (int)w;
                weights[i] = clamped;
                total += clamped;
            }
            if (total <= 0) return null;

            int roll = UnityEngine.Random.Range(0, (int)Mathf.Min(total, int.MaxValue));
            int accum = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                int w = weights[i];
                if (w <= 0) continue;
                accum += w;
                if (roll < accum)
                {
                    return entries[i];
                }
            }
            return entries[entries.Count - 1];
        }

        private static ItemInstance RollItemInstance(int level, ItemDefinition def)
        {
            if (def == null) return null;
            var rolledAffixes = RollAffixes(level, def);
            return new ItemInstance(def, level, rolledAffixes);
        }

        private static List<ItemInstance.AffixRoll> RollAffixes(int level, ItemDefinition def)
        {
            // Determine number of affixes based on rarity
            int min, max;
            switch (def.Rarity)
            {
                case Rarity.Common:
                    min = 0; max = 0; break;
                case Rarity.Magic:
                    min = 1; max = 2; break;
                case Rarity.Rare:
                    min = 2; max = 3; break;
                case Rarity.Legendary:
                    min = 3; max = 4; break;
                default:
                    min = 0; max = 0; break;
            }
            int count = UnityEngine.Random.Range(min, max + 1);
            if (count <= 0) return new List<ItemInstance.AffixRoll>(0);

            var candidates = def.PossibleAffixes;
            if (candidates == null || candidates.Count == 0)
            {
                return new List<ItemInstance.AffixRoll>(0);
            }

            // Prepare weights respecting restrictions; avoid duplicates
            var valid = new List<AffixDefinition>(candidates.Count);
            var weights = new List<int>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var a = candidates[i];
                if (a == null) continue;
                if (!a.IsAllowedFor(def.ItemType, def.AllowedEquipmentSlot)) continue;
                valid.Add(a);
                weights.Add(Mathf.Max(1, a.Weight));
            }
            if (valid.Count == 0) return new List<ItemInstance.AffixRoll>(0);

            var rolls = new List<ItemInstance.AffixRoll>(count);
            for (int i = 0; i < count; i++)
            {
                int idx = SelectIndexWeighted(weights);
                if (idx < 0 || idx >= valid.Count) break;
                var chosen = valid[idx];
                // Prevent duplicate of the same affix definition
                valid.RemoveAt(idx);
                weights.RemoveAt(idx);

                float val = chosen.RollValue();
                // Simple, level-aware scaling hook (light touch). Currently neutral; reserved for future extension.
                // Could do: val *= (1f + (level - 1) * 0.02f);

                rolls.Add(new ItemInstance.AffixRoll(chosen, val));
                if (valid.Count == 0) break;
            }
            return rolls;
        }

        private static int SelectIndexWeighted(List<int> weights)
        {
            long total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                int w = weights[i];
                if (w > 0) total += w;
            }
            if (total <= 0) return -1;
            int roll = UnityEngine.Random.Range(0, (int)Mathf.Min(total, int.MaxValue));
            int accum = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                int w = weights[i];
                if (w <= 0) continue;
                accum += w;
                if (roll < accum) return i;
            }
            return weights.Count - 1;
        }
    }
}


