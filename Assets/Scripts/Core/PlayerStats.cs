using System;
using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Central manager for the Dual-Stat System: Drive (Calories) and Focus (Sleep).
    /// Converts real-world health data into game attributes.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        public const float CaloriesForMaxDrive = 2000f;
        public const float SleepSecondsForMaxFocus = 28800f;

        private static PlayerStats _instance;
        private static readonly object _lock = new object();

        public static PlayerStats Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                lock (_lock)
                {
                    return PersistentSingletonUtility.EnsureInstance(ref _instance, "PlayerStats");
                }
            }
        }

        [Header("Game Stats")]
        [Range(0, 100)]
        [SerializeField] private float currentDrive;
        [Range(0, 100)]
        [SerializeField] private float currentFocus;

        [Header("Raw Data")]
        [SerializeField] private float lastCalories;
        [SerializeField] private float lastSleepSeconds;
        [SerializeField] private bool hasData;

        public float CurrentDrive => currentDrive;
        public float CurrentFocus => currentFocus;
        public float LastCalories => lastCalories;
        public float LastSleepSeconds => lastSleepSeconds;
        public bool HasData => hasData;
        /// <summary>
        /// Latest formatted stats log line, matching the PlayerStats debug output.
        /// </summary>
        public string LastLogLine => !hasData
            ? string.Empty
            : $"[PlayerStats] Updated. Calories: {lastCalories} -> Drive: {currentDrive}. Sleep: {lastSleepSeconds}s -> Focus: {currentFocus}.";

        /// <summary>
        /// Fired after stats are updated.
        /// </summary>
        public event Action OnStatsUpdated;

        private void Awake()
        {
            PersistentSingletonUtility.TryInitialize(this, ref _instance);
        }

        private void OnDestroy()
        {
            PersistentSingletonUtility.ClearIfOwned(this, ref _instance);
        }

        /// <summary>
        /// Updates the game stats based on raw health data.
        /// </summary>
        /// <param name="calories">Active energy burned (kcal).</param>
        /// <param name="sleepSeconds">Sleep duration in seconds.</param>
        public void UpdateStats(float calories, float sleepSeconds)
        {
            lastCalories = calories;
            lastSleepSeconds = sleepSeconds;
            hasData = true;

            currentDrive = CalculateDrive(calories);
            currentFocus = CalculateFocus(sleepSeconds);

            Debug.Log(LastLogLine);
            OnStatsUpdated?.Invoke();
        }

        /// <summary>
        /// Helper to update only calories while keeping existing sleep data.
        /// </summary>
        public void UpdateCalories(float calories)
        {
            UpdateStats(calories, lastSleepSeconds);
        }

        /// <summary>
        /// Helper to update only sleep while keeping existing calorie data.
        /// </summary>
        public void UpdateSleep(float sleepSeconds)
        {
            UpdateStats(lastCalories, sleepSeconds);
        }

        /// <summary>
        /// Converts calories to Drive using the normalized stat mapping.
        /// </summary>
        public static float CalculateDrive(float calories)
        {
            return Mathf.Clamp((calories / CaloriesForMaxDrive) * 100f, 0f, 100f);
        }

        /// <summary>
        /// Converts sleep duration (seconds) to Focus using the normalized stat mapping.
        /// </summary>
        public static float CalculateFocus(float sleepSeconds)
        {
            return Mathf.Clamp((sleepSeconds / SleepSecondsForMaxFocus) * 100f, 0f, 100f);
        }
    }
}
