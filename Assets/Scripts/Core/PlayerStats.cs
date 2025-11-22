using UnityEngine;

namespace Teutoburg.Core
{
    /// <summary>
    /// Central manager for the Dual-Stat System: Fury (Calories) and Clarity (Sleep).
    /// Converts real-world health data into game attributes.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        private static PlayerStats _instance;
        private static readonly object _lock = new object();

        public static PlayerStats Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("PlayerStats");
                            _instance = go.AddComponent<PlayerStats>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("Game Stats")]
        [Range(0, 100)]
        [SerializeField] private float currentFury;
        [Range(0, 100)]
        [SerializeField] private float currentClarity;

        [Header("Raw Data")]
        [SerializeField] private float lastCalories;
        [SerializeField] private float lastSleepSeconds;

        public float CurrentFury => currentFury;
        public float CurrentClarity => currentClarity;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
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

            // Map 2000 calories to 100 Fury
            // Formula: (calories / 2000) * 100, clamped to 0-100
            currentFury = Mathf.Clamp((calories / 2000f) * 100f, 0f, 100f);

            // Map 8 hours (28800 seconds) to 100 Clarity
            // Formula: (sleepSeconds / 28800) * 100, clamped to 0-100
            currentClarity = Mathf.Clamp((sleepSeconds / 28800f) * 100f, 0f, 100f);

            Debug.Log($"[PlayerStats] Updated. Calories: {calories} -> Fury: {currentFury}. Sleep: {sleepSeconds}s -> Clarity: {currentClarity}.");
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
    }
}

