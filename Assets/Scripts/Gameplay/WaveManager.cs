using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WaveManager : MonoBehaviour
{
	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private int startingWave = 1;
	[SerializeField] private bool resetWaveOnPlay = true;
	[SerializeField] private float waveClearDelay = 0.75f;
	[SerializeField] private bool randomizeSpawnPoints = true;

	public static WaveManager Instance { get; private set; }

	private int currentWave;
	private int aliveEnemies;
	private readonly List<EnemyHealth> trackedEnemies = new List<EnemyHealth>();

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);
		if (resetWaveOnPlay)
		{
			currentWave = Mathf.Max(1, startingWave);
		}
	}

	void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// Clear any stale tracking
		UnsubscribeAllTracked();
		aliveEnemies = 0;
		// Spawn enemies for the current wave
		SpawnWave(currentWave <= 0 ? 1 : currentWave);
	}

	private void SpawnWave(int enemyCount)
	{
		if (enemyPrefab == null)
		{
			Debug.LogError("WaveManager: Enemy Prefab is not assigned.");
			return;
		}

		EnemySpawnPoint[] points;
#if UNITY_2023_1_OR_NEWER
		points = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
#else
		points = Object.FindObjectsOfType<EnemySpawnPoint>();
#endif
		if (points == null || points.Length == 0)
		{
			Debug.LogWarning("WaveManager: No EnemySpawnPoint found in the scene. Spawning at origin.");
			for (int i = 0; i < enemyCount; i++)
			{
				SpawnOne(enemyPrefab, Vector3.zero + new Vector3(i * 1.5f, 0f, 0f), Quaternion.identity, null);
			}
			return;
		}

		for (int i = 0; i < enemyCount; i++)
		{
			EnemySpawnPoint point = randomizeSpawnPoints ? points[Random.Range(0, points.Length)] : points[i % points.Length];
			Vector3 pos = point.transform.position;
			if (point.randomRadius > 0f)
			{
				Vector2 rnd = Random.insideUnitCircle * point.randomRadius;
				pos += new Vector3(rnd.x, 0f, rnd.y);
			}
			Quaternion rot = point.transform.rotation;
			SpawnOne(enemyPrefab, pos, rot, point);
		}
	}

	private void SpawnOne(GameObject prefab, Vector3 position, Quaternion rotation, EnemySpawnPoint sourcePoint)
	{
		GameObject go = Instantiate(prefab, position, rotation);
		EnemyHealth health = go.GetComponent<EnemyHealth>();
		if (health == null)
		{
			// Try child if not on root
			health = go.GetComponentInChildren<EnemyHealth>();
		}
		if (health != null)
		{
			trackedEnemies.Add(health);
			health.OnDied += OnEnemyDied;
		}
		aliveEnemies++;
	}

	private void OnEnemyDied()
	{
		// Identify sender and unsubscribe
		EnemyHealth sender = null;
		for (int i = 0; i < trackedEnemies.Count; i++)
		{
			EnemyHealth h = trackedEnemies[i];
			if (h != null && h.IsDead)
			{
				sender = h;
				break;
			}
		}
		if (sender != null)
		{
			sender.OnDied -= OnEnemyDied;
			trackedEnemies.Remove(sender);
		}

		aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
		if (aliveEnemies == 0)
		{
			StartCoroutine(BeginNextWaveAfterDelay());
		}
	}

	private IEnumerator BeginNextWaveAfterDelay()
	{
		if (waveClearDelay > 0f)
		{
			yield return new WaitForSeconds(waveClearDelay);
		}
		currentWave = Mathf.Max(1, currentWave) + 1;
		var scene = SceneManager.GetActiveScene();
		SceneManager.LoadScene(scene.buildIndex);
	}

	private void UnsubscribeAllTracked()
	{
		for (int i = trackedEnemies.Count - 1; i >= 0; i--)
		{
			EnemyHealth h = trackedEnemies[i];
			if (h != null)
			{
				h.OnDied -= OnEnemyDied;
			}
		}
		trackedEnemies.Clear();
	}

	// Optional: call to reset wave number, e.g., on player death from an external script
	public void ResetWavesToStart()
	{
		currentWave = Mathf.Max(1, startingWave);
	}
}


