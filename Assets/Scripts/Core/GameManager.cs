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
                return PersistentSingletonUtility.EnsureInstance(ref _instance, "GameManager");
            }
        }

        private bool isResetting;
        private string sceneToLoad;

        private void Awake()
        {
            if (!PersistentSingletonUtility.TryInitialize(this, ref _instance))
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            PersistentSingletonUtility.ClearIfOwned(this, ref _instance);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isResetting = false;
        }

        /// <summary>
        /// Resets the current run by reloading the active scene.
        /// Also resets level progression back to level 1.
        /// </summary>
        public void ResetRun()
        {
            if (isResetting) return;
            isResetting = true;
            
            // Reset level progression when restarting
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ResetLevels();
            }
            
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
