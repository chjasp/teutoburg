using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private bool isResetting;
        private string sceneToLoad;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isResetting = false;
        }

        /// <summary>
        /// Resets the current run by reloading the active scene.
        /// </summary>
        public void ResetRun()
        {
            if (isResetting) return;
            isResetting = true;
            sceneToLoad = SceneManager.GetActiveScene().name;
            StartCoroutine(ResetRunCoroutine());
        }

        private IEnumerator ResetRunCoroutine()
        {
            yield return null;
            
            // Destroy player and stop animator to prevent animation events on destroyed object
            var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                var animator = player.GetComponentInChildren<Animator>();
                if (animator != null) animator.enabled = false;
                Destroy(player.gameObject);
            }
            
            yield return null;
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
