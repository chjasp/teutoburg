using UnityEngine;

namespace Axiom.Core
{
    public enum EnemyTier
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>
    /// Central tuning knobs for combat balance and stat-driven scaling.
    /// </summary>
    public static class CombatTuning
    {
        // Defaults used until health data arrives.
        public const float DefaultDrive = 50f;
        public const float DefaultFocus = 50f;

        // Player health scaling.
        public const int PlayerBaseHealth = 100;
        public const int PlayerBonusHealthAt100Wellness = 100; // 100 Wellness => +100 HP (200 total)
        public const float PlayerFinalHealthMultiplier = 3f;

        // Enemy time-to-kill target for Hard enemies at level 1.
        public const int HardEnemyTargetTtkSeconds = 60;
        public const int BaselinePlayerDpsForTtk = 100; // assumes ~100 dmg/sec at 100% stats

        // Enemy tier distribution weights.
        public const float EasyTierWeight = 0.5f;
        public const float MediumTierWeight = 0.35f;
        public const float HardTierWeight = 0.15f;

        // Enemy tier health multipliers (relative to Hard).
        public const float EasyHealthMultiplier = 0.35f;
        public const float MediumHealthMultiplier = 0.6f;
        public const float HardHealthMultiplier = 1.0f;

        // Enemy tier damage scaling.
        public const float EnemyBaseDamageScale = 0.5f; // scales prefab damage down to fit new health values
        public const float EasyDamageMultiplier = 0.7f;
        public const float MediumDamageMultiplier = 1.0f;
        public const float HardDamageMultiplier = 1.4f;
        public const float EnemyDamageMultiplierPerLevel = 1.0f; // keep damage flat across levels by default

        public static float GetDrive()
        {
            if (PlayerStats.Instance != null && PlayerStats.Instance.HasData) return PlayerStats.Instance.CurrentDrive;
            return DefaultDrive;
        }

        public static float GetFocus()
        {
            if (PlayerStats.Instance != null && PlayerStats.Instance.HasData) return PlayerStats.Instance.CurrentFocus;
            return DefaultFocus;
        }

        public static float GetWellness()
        {
            return (GetDrive() + GetFocus()) * 0.5f;
        }

        public static int GetPlayerMaxHealth()
        {
            float wellness = GetWellness();
            float bonus = PlayerBonusHealthAt100Wellness * (wellness / 100f);
            float scaled = (PlayerBaseHealth + bonus) * PlayerFinalHealthMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        public static int CalculateStatScaledDamage(float statValue, int baseDamage, float factor)
        {
            float scaled = baseDamage + statValue * factor;
            return Mathf.Clamp(Mathf.RoundToInt(scaled), 0, 100000);
        }

        public static int GetEnemyMaxHealth(EnemyTier tier, int baseMaxHealth, float levelHealthMultiplier)
        {
            baseMaxHealth = Mathf.Max(1, baseMaxHealth);
            float scale = GetGlobalEnemyHealthScale(baseMaxHealth);
            float tierMult = GetTierHealthMultiplier(tier);
            float final = baseMaxHealth * scale * tierMult * levelHealthMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(final));
        }

        public static int GetEnemyAttackDamage(EnemyTier tier, int baseDamage, int level)
        {
            baseDamage = Mathf.Max(0, baseDamage);
            float tierMult = GetTierDamageMultiplier(tier);
            float levelMult = Mathf.Pow(EnemyDamageMultiplierPerLevel, Mathf.Max(0, level - 1));
            float final = baseDamage * EnemyBaseDamageScale * tierMult * levelMult;
            return Mathf.Max(1, Mathf.RoundToInt(final));
        }

        public static EnemyTier PickTier(float roll01)
        {
            float total = EasyTierWeight + MediumTierWeight + HardTierWeight;
            if (total <= 0f) return EnemyTier.Medium;
            float roll = Mathf.Clamp01(roll01) * total;
            if (roll < EasyTierWeight) return EnemyTier.Easy;
            if (roll < EasyTierWeight + MediumTierWeight) return EnemyTier.Medium;
            return EnemyTier.Hard;
        }

        public static float GetTierHealthMultiplier(EnemyTier tier)
        {
            switch (tier)
            {
                case EnemyTier.Easy:
                    return EasyHealthMultiplier;
                case EnemyTier.Medium:
                    return MediumHealthMultiplier;
                default:
                    return HardHealthMultiplier;
            }
        }

        public static float GetTierDamageMultiplier(EnemyTier tier)
        {
            switch (tier)
            {
                case EnemyTier.Easy:
                    return EasyDamageMultiplier;
                case EnemyTier.Medium:
                    return MediumDamageMultiplier;
                default:
                    return HardDamageMultiplier;
            }
        }

        private static float GetGlobalEnemyHealthScale(int baseMaxHealth)
        {
            float hardTarget = HardEnemyTargetTtkSeconds * BaselinePlayerDpsForTtk;
            float denom = baseMaxHealth * HardHealthMultiplier;
            return denom > 0f ? hardTarget / denom : 1f;
        }
    }
}
