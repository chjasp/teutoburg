using UnityEngine;

namespace Teutoburg.Core
{
    public enum GameState
    {
        Hearth,
        Hinterlands,
        Abyss
    }

    /// <summary>
    /// Manages the global game state and progression (Depth).
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

        [Header("Progression")]
        [SerializeField] private int currentDepth = 0;
        [SerializeField] private GameState currentState = GameState.Hearth;

        public int CurrentDepth => currentDepth;
        public GameState CurrentState => currentState;

        public event System.Action<int> OnDepthChanged;

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
        /// Advances the depth by 1 and updates the GameState.
        /// </summary>
        public void IncrementDepth()
        {
            currentDepth++;
            UpdateState();
            Debug.Log($"[GameManager] Depth increased to {currentDepth}. State: {currentState}");
            OnDepthChanged?.Invoke(currentDepth);
        }

        public void ResetRun()
        {
            currentDepth = 0;
            UpdateState();
            OnDepthChanged?.Invoke(currentDepth);
        }

        private void UpdateState()
        {
            if (currentDepth == 0)
            {
                currentState = GameState.Hearth;
            }
            else if (currentDepth >= 1 && currentDepth < 10)
            {
                currentState = GameState.Hinterlands;
            }
            else
            {
                currentState = GameState.Abyss;
            }
        }
    }
}

