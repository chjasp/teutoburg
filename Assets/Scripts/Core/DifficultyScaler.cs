using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Calculates enemy difficulty scaling based on Player Stats (Drive).
    /// </summary>
    public class DifficultyScaler : MonoBehaviour
    {
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
        /// Returns a multiplier for enemy stats.
        /// High Drive (active calories) makes you stronger relative to enemies.
        /// </summary>
        public float GetDifficultyMultiplier()
        {
            float drive = 0f;
            if (PlayerStats.Instance != null)
            {
                drive = PlayerStats.Instance.CurrentDrive;
            }

            // Drive (0-100).
            // At 100 Drive, you're at full strength (multiplier = 1.0).
            // At 0 Drive, enemies feel relatively tougher (multiplier = 1.5).
            // This means high activity days make the game feel easier.
            float mitigationFactor = 1.0f + ((100f - drive) / 200f); // Range: 1.0 to 1.5

            return mitigationFactor;
        }
    }
}
