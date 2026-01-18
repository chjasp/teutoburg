using System.Collections;
using UnityEngine;

namespace Axiom.Phygital
{
    /// <summary>
    /// Spawns visual effects based on the food analysis score.
    /// Higher scores produce bigger, more impressive effects.
    /// </summary>
    [DisallowMultipleComponent]
    public class SacrificeBuffEffect : MonoBehaviour
    {
        [Header("Effect Prefabs")]
        [Tooltip("Base explosion/particle effect prefab. Will be scaled based on score.")]
        [SerializeField] private GameObject _effectPrefab;

        [Tooltip("Optional: Different effect for excellent scores (0.9+).")]
        [SerializeField] private GameObject _excellentEffectPrefab;

        [Tooltip("Optional: Different effect for poor scores (below 0.3).")]
        [SerializeField] private GameObject _poorEffectPrefab;

        [Header("Scaling")]
        [Tooltip("Minimum scale multiplier (for score = 0).")]
        [SerializeField] private float _minScale = 0.5f;

        [Tooltip("Maximum scale multiplier (for score = 1).")]
        [SerializeField] private float _maxScale = 2.5f;

        [Header("Positioning")]
        [Tooltip("Transform where the effect should spawn. If null, uses player position.")]
        [SerializeField] private Transform _effectSpawnPoint;

        [Tooltip("Height offset above the spawn point.")]
        [SerializeField] private float _heightOffset = 1f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip _buffSound;
        [SerializeField] private AudioSource _audioSource;

        [Header("Screen Flash (Optional)")]
        [SerializeField] private CanvasGroup _flashOverlay;
        [SerializeField] private float _flashDuration = 0.3f;

        private static SacrificeBuffEffect _instance;

        /// <summary>
        /// Singleton instance for easy access.
        /// </summary>
        public static SacrificeBuffEffect Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SacrificeBuffEffect>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        /// <summary>
        /// Triggers the buff visual effect based on the food analysis score.
        /// </summary>
        /// <param name="score">Health score from 0.0 (unhealthy) to 1.0 (healthy).</param>
        /// <param name="category">Category string from the API (excellent, good, etc.).</param>
        public void TriggerEffect(float score, string category = "")
        {
            score = Mathf.Clamp01(score);

            // Determine spawn position
            Vector3 spawnPos = GetSpawnPosition();

            // Select appropriate prefab
            GameObject prefabToUse = SelectPrefab(score, category);

            if (prefabToUse == null)
            {
                Debug.LogWarning("[SacrificeBuffEffect] No effect prefab assigned!");
                return;
            }

            // Calculate scale based on score
            float scaleMult = Mathf.Lerp(_minScale, _maxScale, score);

            // Spawn the effect
            var effect = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
            effect.transform.localScale *= scaleMult;

            // Auto-destroy after some time (particle systems usually handle this themselves)
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(effect, duration + 1f);
            }
            else
            {
                Destroy(effect, 5f); // Fallback
            }

            // Play sound
            PlayBuffSound(score);

            // Optional screen flash
            if (_flashOverlay != null)
            {
                StartCoroutine(ScreenFlashCoroutine(score));
            }

            Debug.Log($"[SacrificeBuffEffect] Triggered effect - Score: {score:F2}, Scale: {scaleMult:F2}x, Category: {category}");
        }

        private Vector3 GetSpawnPosition()
        {
            if (_effectSpawnPoint != null)
            {
                return _effectSpawnPoint.position + Vector3.up * _heightOffset;
            }

            // Try to find player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                return player.transform.position + Vector3.up * _heightOffset;
            }

            // Fallback to this object's position
            return transform.position + Vector3.up * _heightOffset;
        }

        private GameObject SelectPrefab(float score, string category)
        {
            // Use category-specific prefabs if available
            if (score >= 0.9f && _excellentEffectPrefab != null)
            {
                return _excellentEffectPrefab;
            }

            if (score < 0.3f && _poorEffectPrefab != null)
            {
                return _poorEffectPrefab;
            }

            return _effectPrefab;
        }

        private void PlayBuffSound(float score)
        {
            if (_buffSound == null) return;

            AudioSource source = _audioSource;
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            if (source != null)
            {
                // Vary pitch slightly based on score (higher score = higher pitch)
                source.pitch = Mathf.Lerp(0.8f, 1.2f, score);
                source.PlayOneShot(_buffSound);
            }
            else
            {
                // Fallback: play at position
                AudioSource.PlayClipAtPoint(_buffSound, GetSpawnPosition());
            }
        }

        private IEnumerator ScreenFlashCoroutine(float score)
        {
            if (_flashOverlay == null) yield break;

            // Flash intensity based on score
            float targetAlpha = Mathf.Lerp(0.1f, 0.5f, score);

            // Flash in
            float elapsed = 0f;
            float halfDuration = _flashDuration / 2f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                _flashOverlay.alpha = Mathf.Lerp(0f, targetAlpha, elapsed / halfDuration);
                yield return null;
            }

            // Flash out
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                _flashOverlay.alpha = Mathf.Lerp(targetAlpha, 0f, elapsed / halfDuration);
                yield return null;
            }

            _flashOverlay.alpha = 0f;
        }

        /// <summary>
        /// Test method to trigger effect directly from inspector or code.
        /// </summary>
        [ContextMenu("Test Effect (Score 0.5)")]
        public void TestEffectMedium()
        {
            TriggerEffect(0.5f, "moderate");
        }

        [ContextMenu("Test Effect (Score 1.0)")]
        public void TestEffectExcellent()
        {
            TriggerEffect(1.0f, "excellent");
        }

        [ContextMenu("Test Effect (Score 0.2)")]
        public void TestEffectPoor()
        {
            TriggerEffect(0.2f, "poor");
        }
    }
}
