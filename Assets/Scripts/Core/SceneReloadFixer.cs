using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Axiom.Core
{
    /// <summary>
    /// Ensures proper UI state after scene reload by handling duplicate EventSystems
    /// and refreshing Canvas references.
    /// </summary>
    public class SceneReloadFixer : MonoBehaviour
    {
        private static SceneReloadFixer _instance;

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
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Fix duplicate EventSystems - keep only one
            FixDuplicateEventSystems();
            
            // Refresh Canvas camera references
            RefreshCanvasReferences();
        }

        private void FixDuplicateEventSystems()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            
            if (eventSystems.Length > 1)
            {
                Debug.Log($"[SceneReloadFixer] Found {eventSystems.Length} EventSystems, keeping only one.");
                
                // Keep the first one, destroy the rest
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    Destroy(eventSystems[i].gameObject);
                }
            }
            
            // Ensure the remaining EventSystem is enabled and has a valid input module
            if (eventSystems.Length > 0 && eventSystems[0] != null)
            {
                eventSystems[0].enabled = true;
                
                // Re-enable input modules
                var inputModules = eventSystems[0].GetComponents<BaseInputModule>();
                foreach (var module in inputModules)
                {
                    module.enabled = false;
                    module.enabled = true;
                }
            }
        }

        private void RefreshCanvasReferences()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            
            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;
                
                // If canvas uses Screen Space - Camera, ensure camera reference is valid
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    if (canvas.worldCamera == null)
                    {
                        canvas.worldCamera = Camera.main;
                        Debug.Log($"[SceneReloadFixer] Fixed camera reference for Canvas '{canvas.name}'");
                    }
                }
            }
        }
    }
}
