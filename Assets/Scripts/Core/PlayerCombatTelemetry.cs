using System;
using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Lightweight combat telemetry feed for recent player attack behavior.
    /// </summary>
    public static class PlayerCombatTelemetry
    {
        public const string AttackStyleNone = "none";
        public const string AttackStyleMelee = "melee";
        public const string AttackStyleRanged = "ranged";

        public static event Action<string> AttackStyleReported;

        public static string LastAttackStyle { get; private set; } = AttackStyleNone;
        public static float LastAttackTimestamp { get; private set; } = -999f;

        /// <summary>
        /// Reports that the player performed a melee attack.
        /// </summary>
        public static void ReportMeleeAttack()
        {
            ReportAttackStyle(AttackStyleMelee);
        }

        /// <summary>
        /// Reports that the player performed a ranged attack.
        /// </summary>
        public static void ReportRangedAttack()
        {
            ReportAttackStyle(AttackStyleRanged);
        }

        /// <summary>
        /// Clears the last known attack style.
        /// </summary>
        public static void Reset()
        {
            LastAttackStyle = AttackStyleNone;
            LastAttackTimestamp = -999f;
        }

        private static void ReportAttackStyle(string style)
        {
            LastAttackStyle = string.IsNullOrWhiteSpace(style) ? AttackStyleNone : style;
            LastAttackTimestamp = Time.time;
            AttackStyleReported?.Invoke(LastAttackStyle);
        }
    }
}
