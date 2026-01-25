using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Core
{
    /// <summary>
    /// Manages level progression. Tracks enemy count and advances levels when all enemies are defeated.
    /// Each level increases enemy health by 20% (compounding).
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        private static LevelManager _instance;
        public static LevelManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("LevelManager");
                    _instance = go.AddComponent<LevelManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private float healthMultiplierPerLevel = 1.2f;
        [SerializeField] private float levelTransitionDelay = 1.5f;

        /// <summary>
        /// Current level (starts at 1).
        /// </summary>
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>
        /// Fired when the level changes. Passes the new level number.
        /// </summary>
        public event Action<int> OnLevelChanged;

        private int _enemyCount;
        private int _totalEnemiesRegistered; // Track how many enemies registered this level
        private bool _isTransitioning;
        private bool _levelStarted; // Prevents checking completion until first frame after load
        private Vector3 _level1SpawnPosition;
        private Quaternion _level1SpawnRotation;
        private bool _hasLevel1Spawn;

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
            // Reset transition flag but DON'T reset enemy count - enemies register in Awake() before this callback
            _isTransitioning = false;
            _levelStarted = false;
            
            Debug.Log($"[LevelManager] Scene '{scene.name}' loaded for Level {CurrentLevel}. Enemies registered so far: {_enemyCount}");
            
            // Use Invoke for safer timing - give one frame for everything to initialize
            Invoke(nameof(StartLevelDelayed), 0.1f);
        }

        private void StartLevelDelayed()
        {
            CacheLevel1SpawnIfNeeded();
            ResetPlayerToLevel1Spawn();
            ApplyPlayerCombatTuning();
            AssignEnemyTiersAndStats();

            // Count enemies directly as the authoritative source
            var enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            int actualEnemyCount = 0;
            foreach (var e in enemies)
            {
                if (e != null && !e.IsDead) actualEnemyCount++;
            }
            
            // Use actual count from scene - this is the ground truth
            _enemyCount = actualEnemyCount;
            _totalEnemiesRegistered = _enemyCount;
            _levelStarted = true;
            
            // Notify UI of current level
            OnLevelChanged?.Invoke(CurrentLevel);
            
            Debug.Log($"[LevelManager] Level {CurrentLevel} STARTED with {_enemyCount} enemies (Health multiplier: {GetHealthMultiplier():F2}x)");
        }

        /// <summary>
        /// Returns the health multiplier for the current level.
        /// Level 1 = 1.0x, Level 2 = 1.2x, Level 3 = 1.44x, etc.
        /// </summary>
        public float GetHealthMultiplier()
        {
            return Mathf.Pow(healthMultiplierPerLevel, CurrentLevel - 1);
        }

        /// <summary>
        /// Called by EnemyHealth when an enemy spawns/awakens.
        /// </summary>
        public void RegisterEnemy()
        {
            _enemyCount++;
        }

        /// <summary>
        /// Called by EnemyHealth when an enemy dies.
        /// </summary>
        public void UnregisterEnemy()
        {
            _enemyCount--;
            
            // Only check for level completion after the level has properly started
            // and we had at least one enemy to begin with
            if (_levelStarted && _totalEnemiesRegistered > 0 && _enemyCount <= 0 && !_isTransitioning)
            {
                _enemyCount = 0;
                StartCoroutine(TransitionToNextLevel());
            }
        }

        private IEnumerator TransitionToNextLevel()
        {
            _isTransitioning = true;
            _levelStarted = false; // Prevent further checks during transition
            
            Debug.Log($"[LevelManager] All enemies defeated! Transitioning to level {CurrentLevel + 1} in {levelTransitionDelay}s...");
            
            yield return new WaitForSeconds(levelTransitionDelay);
            
            // Advance to next level
            CurrentLevel++;
            
            // Reset enemy tracking BEFORE loading - new enemies will register fresh
            _enemyCount = 0;
            _totalEnemiesRegistered = 0;
            
            // Reload the current scene
            string currentScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(currentScene);
        }

        /// <summary>
        /// Resets level progression back to level 1 (call on game over/restart).
        /// </summary>
        public void ResetLevels()
        {
            CurrentLevel = 1;
            _enemyCount = 0;
            _totalEnemiesRegistered = 0;
            _isTransitioning = false;
            _levelStarted = false;
            _hasLevel1Spawn = false;
            OnLevelChanged?.Invoke(CurrentLevel);
        }

        /// <summary>
        /// Returns the current enemy count (for debugging).
        /// </summary>
        public int GetEnemyCount() => _enemyCount;

        private void CacheLevel1SpawnIfNeeded()
        {
            if (_hasLevel1Spawn || CurrentLevel != 1) return;

            var player = FindFirstObjectByType<PlayerHealth>();
            if (player == null) return;

            _level1SpawnPosition = player.transform.position;
            _level1SpawnRotation = player.transform.rotation;
            _hasLevel1Spawn = true;
        }

        private void ResetPlayerToLevel1Spawn()
        {
            if (!_hasLevel1Spawn) return;

            var player = FindFirstObjectByType<PlayerHealth>();
            if (player == null) return;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            player.transform.SetPositionAndRotation(_level1SpawnPosition, _level1SpawnRotation);

            if (controller != null) controller.enabled = true;
        }

        private void ApplyPlayerCombatTuning()
        {
            var player = FindFirstObjectByType<PlayerHealth>();
            if (player == null) return;

            int maxHealth = CombatTuning.GetPlayerMaxHealth();
            player.SetMaxHealth(maxHealth, true);
        }

        private void AssignEnemyTiersAndStats()
        {
            var enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            if (enemies == null || enemies.Length == 0) return;

            int autoCount = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && !enemies[i].UseAssignedTier) autoCount++;
            }

            var tiers = BuildTierList(autoCount);
            if (tiers.Count > 1) Shuffle(tiers);

            float levelMultiplier = GetHealthMultiplier();
            int autoIndex = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null) continue;
                if (enemies[i].UseAssignedTier)
                {
                    enemies[i].ApplyTierAndStats(enemies[i].AssignedTier, levelMultiplier, CurrentLevel);
                }
                else
                {
                    var pickedTier = tiers.Count > 0 && autoIndex < tiers.Count
                        ? tiers[autoIndex++]
                        : CombatTuning.PickTier(UnityEngine.Random.value);
                    enemies[i].ApplyTierAndStats(pickedTier, levelMultiplier, CurrentLevel);
                }
            }
        }

        private static List<EnemyTier> BuildTierList(int count)
        {
            var tiers = new List<EnemyTier>(count);

            if (count >= 3)
            {
                tiers.Add(EnemyTier.Easy);
                tiers.Add(EnemyTier.Medium);
                tiers.Add(EnemyTier.Hard);
            }

            while (tiers.Count < count)
            {
                tiers.Add(CombatTuning.PickTier(UnityEngine.Random.value));
            }

            return tiers;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
