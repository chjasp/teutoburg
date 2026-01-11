using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Manages the global game state.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

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
        /// Resets the current run state.
        /// </summary>
        public void ResetRun()
        {
            // Placeholder for future run-reset logic (respawn, stats reset, etc.)
            Debug.Log("[GameManager] Run reset.");
        }
    }
}
