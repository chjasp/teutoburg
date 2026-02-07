using System.Collections.Generic;
using Axiom.Core;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ZoneEnemyDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ZoneRouteNetwork _routeNetwork;
    [SerializeField] private Transform _enemyRoot;

    [Header("Drone Prefabs")]
    [SerializeField] private GameObject _basicDronePrefab;
    [SerializeField] private GameObject _flankerDronePrefab;
    [SerializeField] private GameObject _heavyDronePrefab;

    [Header("Spawn")]
    [SerializeField] private float _spawnScatterRadius = 4f;
    [SerializeField] private int _heavyBonusHealth = 180;

    private readonly List<ZoneDroneController> _allDrones = new List<ZoneDroneController>(64);

    /// <summary>
    /// Initializes spawn references for zone-controlled enemies.
    /// </summary>
    public void Initialize(ZoneRouteNetwork routeNetwork, Transform enemyRoot)
    {
        _routeNetwork = routeNetwork;
        _enemyRoot = enemyRoot;

        if (_enemyRoot == null)
        {
            GameObject go = new GameObject("ZoneSpawnedEnemies");
            go.transform.SetParent(transform, false);
            _enemyRoot = go.transform;
        }

        AutoResolvePrefabsFromScene();
    }

    /// <summary>
    /// Spawns all defender squads for Alpha/Bravo/Charlie.
    /// </summary>
    public void SpawnInitialDefenderSquads()
    {
        SpawnSquad(ZoneId.Alpha, 3, DroneRole.Basic, 0, DroneRole.Flanker, 0, DroneRole.Heavy);
        SpawnSquad(ZoneId.Bravo, 3, DroneRole.Basic, 2, DroneRole.Flanker, 0, DroneRole.Heavy);
        SpawnSquad(ZoneId.Charlie, 4, DroneRole.Basic, 2, DroneRole.Flanker, 1, DroneRole.Heavy);
    }

    /// <summary>
    /// Spawns a reinforcement squad from one zone and orders it to another.
    /// </summary>
    public List<ZoneDroneController> SpawnReinforcementSquad(ZoneId originZoneId, ZoneId targetZoneId, int squadSize)
    {
        squadSize = Mathf.Clamp(squadSize, 1, 8);

        var squad = new List<ZoneDroneController>(squadSize);
        for (int i = 0; i < squadSize; i++)
        {
            DroneRole role = PickReinforcementRole(i);
            ZoneDroneController drone = SpawnDrone(originZoneId, role);
            if (drone == null)
            {
                continue;
            }

            drone.IssueRelocationOrder(targetZoneId);
            squad.Add(drone);
        }

        return squad;
    }

    /// <summary>
    /// Returns all active drone controllers.
    /// </summary>
    public IReadOnlyList<ZoneDroneController> GetAllDrones()
    {
        PruneDeadEntries();
        return _allDrones;
    }

    /// <summary>
    /// Returns alive drones currently assigned to a zone objective.
    /// </summary>
    public List<ZoneDroneController> GetAliveDronesByActiveZone(ZoneId zoneId)
    {
        PruneDeadEntries();

        var result = new List<ZoneDroneController>(16);
        for (int i = 0; i < _allDrones.Count; i++)
        {
            ZoneDroneController drone = _allDrones[i];
            if (drone == null || !drone.IsAlive)
            {
                continue;
            }

            if (drone.ActiveZoneId == zoneId)
            {
                result.Add(drone);
            }
        }

        return result;
    }

    private void SpawnSquad(
        ZoneId zoneId,
        int primaryCount,
        DroneRole primaryRole,
        int secondaryCount,
        DroneRole secondaryRole,
        int tertiaryCount,
        DroneRole tertiaryRole)
    {
        for (int i = 0; i < primaryCount; i++)
        {
            SpawnDrone(zoneId, primaryRole);
        }

        for (int i = 0; i < secondaryCount; i++)
        {
            SpawnDrone(zoneId, secondaryRole);
        }

        for (int i = 0; i < tertiaryCount; i++)
        {
            SpawnDrone(zoneId, tertiaryRole);
        }
    }

    private ZoneDroneController SpawnDrone(ZoneId zoneId, DroneRole role)
    {
        if (_routeNetwork == null)
        {
            Debug.LogWarning("[ZoneEnemyDirector] Missing ZoneRouteNetwork; cannot spawn drone.");
            return null;
        }

        GameObject prefab = GetPrefab(role);
        if (prefab == null)
        {
            Debug.LogWarning($"[ZoneEnemyDirector] Missing prefab for role {role}.");
            return null;
        }

        Vector3 zoneCenter = _routeNetwork.GetZoneCenter(zoneId);
        Vector2 scatter = Random.insideUnitCircle * _spawnScatterRadius;
        Vector3 spawnPos = zoneCenter + new Vector3(scatter.x, 0f, scatter.y);

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject instance = Instantiate(prefab, spawnPos, rotation, _enemyRoot);
        instance.name = $"{zoneId}_{role}_{instance.name}";

        ZoneDroneController controller = instance.GetComponent<ZoneDroneController>();
        if (controller == null)
        {
            controller = instance.AddComponent<ZoneDroneController>();
        }

        controller.Initialize(_routeNetwork, zoneId, zoneCenter, role);

        if (role == DroneRole.Heavy)
        {
            ApplyHeavyStats(instance);
        }

        _allDrones.Add(controller);
        return controller;
    }

    private void ApplyHeavyStats(GameObject drone)
    {
        if (drone == null)
        {
            return;
        }

        EnemyHealth health = drone.GetComponent<EnemyHealth>();
        if (health != null && !health.IsDead)
        {
            int newMax = Mathf.Max(1, health.MaxHealth + _heavyBonusHealth);
            health.SetMaxHealth(newMax, true);
        }

        IEnemyAttackTuning attackTuning = drone.GetComponent<IEnemyAttackTuning>();
        if (attackTuning != null)
        {
            attackTuning.SetAttackDamage(Mathf.RoundToInt(attackTuning.BaseAttackDamage * 1.2f));
        }
    }

    private GameObject GetPrefab(DroneRole role)
    {
        switch (role)
        {
            case DroneRole.Flanker:
                return _flankerDronePrefab != null ? _flankerDronePrefab : _basicDronePrefab;
            case DroneRole.Heavy:
                return _heavyDronePrefab != null ? _heavyDronePrefab : _flankerDronePrefab;
            default:
                return _basicDronePrefab;
        }
    }

    private DroneRole PickReinforcementRole(int index)
    {
        if (index == 0)
        {
            return DroneRole.Flanker;
        }

        float roll = Random.value;
        if (roll < 0.18f)
        {
            return DroneRole.Heavy;
        }

        return roll < 0.45f ? DroneRole.Flanker : DroneRole.Basic;
    }

    private void AutoResolvePrefabsFromScene()
    {
        if (_basicDronePrefab == null)
        {
            HunterDroneAI hunter = FindFirstObjectByType<HunterDroneAI>(FindObjectsInactive.Include);
            if (hunter != null)
            {
                _basicDronePrefab = hunter.gameObject;
            }
        }

        if (_flankerDronePrefab == null)
        {
            SuppressionDroneAI suppression = FindFirstObjectByType<SuppressionDroneAI>(FindObjectsInactive.Include);
            if (suppression != null)
            {
                _flankerDronePrefab = suppression.gameObject;
            }
        }

        if (_heavyDronePrefab == null)
        {
            DisruptorDroneAI disruptor = FindFirstObjectByType<DisruptorDroneAI>(FindObjectsInactive.Include);
            if (disruptor != null)
            {
                _heavyDronePrefab = disruptor.gameObject;
            }
        }

        if (_flankerDronePrefab == null)
        {
            _flankerDronePrefab = _basicDronePrefab;
        }

        if (_heavyDronePrefab == null)
        {
            _heavyDronePrefab = _flankerDronePrefab != null ? _flankerDronePrefab : _basicDronePrefab;
        }
    }

    private void PruneDeadEntries()
    {
        for (int i = _allDrones.Count - 1; i >= 0; i--)
        {
            ZoneDroneController drone = _allDrones[i];
            if (drone == null || !drone.gameObject || !drone.gameObject.activeInHierarchy || !drone.IsAlive)
            {
                _allDrones.RemoveAt(i);
            }
        }
    }
}
