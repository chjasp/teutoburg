using UnityEngine;

namespace Teutoburg.Core
{
    /// <summary>
    /// Calculates enemy difficulty scaling based on Depth and Player Stats (Fury).
    /// </summary>
    public class DifficultyScaler : MonoBehaviour
    {
        // Singleton convenience, though could be attached to GameManager
        private static DifficultyScaler _instance;
        public static DifficultyScaler Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DifficultyScaler");
                    _instance = go.AddComponent<DifficultyScaler>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            else if (_instance != this) Destroy(this);
        }

        /// <summary>
        /// Returns a multiplier > 1.0f for enemy stats.
        /// Base scaling comes from Depth.
        /// High Fury (active calories) acts as a 'buffer' making you stronger relative to the enemies.
        /// </summary>
        public float GetDifficultyMultiplier()
        {
            int depth = GameManager.Instance != null ? GameManager.Instance.CurrentDepth : 0;
            float depthScale = 1.0f + (depth * 0.1f);

            // Integration:
            // Concept: "Depth acts as a fitness gate."
            // If Fury is high, the effective difficulty feels "normal" (or easier).
            // If Fury is low, the difficulty multiplier applies fully or is exacerbated.
            
            float fury = 0f;
            if (PlayerStats.Instance != null)
            {
                fury = PlayerStats.Instance.CurrentFury;
            }

            // Logic:
            // Fury (0-100).
            // Let's say at 100 Fury, you "negate" some of the depth scaling or get a flat bonus.
            // Implementation: We divide the difficulty by a factor derived from Fury.
            // Example: 
            // Fury 0 -> Multiplier = depthScale / 1.0 = depthScale
            // Fury 100 -> Multiplier = depthScale / 1.5 = 66% of raw power
            
            // This means high activity days make deep runs "easier" (you are stronger).
            float mitigationFactor = 1.0f + (fury / 200f); // Range: 1.0 to 1.5

            return depthScale / mitigationFactor;
        }
    }
}

