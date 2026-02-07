using System;
using System.Collections.Generic;
using Axiom.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ZoneControlGameMode : MonoBehaviour
{
    [Header("Scene Scope")]
    [SerializeField] private bool _onlyInCitadelScene = true;
    [SerializeField] private string _citadelSceneName = "Citadel";
    [SerializeField] private bool _disableLegacyEnemiesRoot = true;
    [SerializeField] private string _legacyEnemiesRootName = "Enemies";

    [Header("Capture Tuning")]
    [SerializeField] private float _zoneRadius = 10f;
    [SerializeField] private float _playerCaptureDuration = 8f;
    [SerializeField] private float _enemyRecaptureDuration = 12f;
    [SerializeField] private float _progressDecayDuration = 5f;

    [Header("Win/Lose")]
    [SerializeField] private float _holdAllZonesDuration = 30f;

    [Header("Default Runtime Layout")]
    [SerializeField] private Vector3 _alphaDefaultCenter = new Vector3(-17f, 0f, -17f);
    [SerializeField] private Vector3 _bravoDefaultCenter = new Vector3(-7f, 0f, -2f);
    [SerializeField] private Vector3 _charlieDefaultCenter = new Vector3(-20f, 0f, 12f);

    [Header("References")]
    [SerializeField] private Transform _zoneRoot;
    [SerializeField] private Transform _enemyRoot;
    [SerializeField] private CapturableZone[] _zones = Array.Empty<CapturableZone>();
    [SerializeField] private ZoneRouteNetwork _routeNetwork;
    [SerializeField] private ZoneCoverGenerator _coverGenerator;
    [SerializeField] private ZoneEnemyDirector _enemyDirector;
    [SerializeField] private ReinforcementManager _reinforcementManager;
    [SerializeField] private ZoneHUDController _zoneHUDController;
    [SerializeField] private ZoneEndScreenUI _zoneEndScreenUI;

    private readonly Dictionary<ZoneId, CapturableZone> _zoneLookup = new Dictionary<ZoneId, CapturableZone>();

    private bool _isInitialized;
    private bool _isMatchEnded;
    private ZoneHoldTimerState _holdTimer;
    private GameObject _cachedLegacyEnemiesRoot;

    public bool IsMatchEnded => _isMatchEnded;

    private void Awake()
    {
        if (_onlyInCitadelScene)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!string.Equals(sceneName, _citadelSceneName, StringComparison.Ordinal))
            {
                enabled = false;
                return;
            }
        }

        LevelManager.SuppressLevelProgression = true;

        EnsureDependencies();
        EnsureRuntimeZoneLayout();

        if (_coverGenerator != null)
        {
            _coverGenerator.GenerateCover();
        }

        BuildZoneLookup();
        SubscribeZoneEvents();

        _enemyDirector.Initialize(_routeNetwork, _enemyRoot);
        _enemyDirector.SpawnInitialDefenderSquads();

        if (_disableLegacyEnemiesRoot)
        {
            DisableLegacyEnemiesRoot();
        }

        _reinforcementManager.Initialize(this, _enemyDirector);
        _zoneHUDController.Initialize(_zones);

        _zoneEndScreenUI.Initialize();
        _zoneEndScreenUI.Hide();

        PlayerHealth.DeathHandledExternally = HandleExternalPlayerDeath;

        _holdTimer = new ZoneHoldTimerState(_holdAllZonesDuration);

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        if (PlayerHealth.DeathHandledExternally == HandleExternalPlayerDeath)
        {
            PlayerHealth.DeathHandledExternally = null;
        }

        UnsubscribeZoneEvents();

        if (_reinforcementManager != null)
        {
            _reinforcementManager.CancelAll();
        }

        LevelManager.SuppressLevelProgression = false;
    }

    private void Update()
    {
        if (_disableLegacyEnemiesRoot)
        {
            DisableLegacyEnemiesRoot();
        }

        if (!_isInitialized || _isMatchEnded)
        {
            return;
        }

        UpdateZoneHUDStates();
        UpdateCaptureContext();
        UpdateHoldTimer();
    }

    /// <summary>
    /// Returns a zone by id.
    /// </summary>
    public CapturableZone GetZone(ZoneId zoneId)
    {
        _zoneLookup.TryGetValue(zoneId, out CapturableZone zone);
        return zone;
    }

    /// <summary>
    /// Picks a random enemy-owned zone, excluding the provided id.
    /// </summary>
    public ZoneId? PickRandomEnemyOwnedZone(ZoneId excludeZoneId)
    {
        var candidates = new List<ZoneId>(3);

        for (int i = 0; i < _zones.Length; i++)
        {
            CapturableZone zone = _zones[i];
            if (zone == null || zone.Id == excludeZoneId)
            {
                continue;
            }

            if (zone.Ownership == ZoneOwnership.Enemy)
            {
                candidates.Add(zone.Id);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void UpdateZoneHUDStates()
    {
        for (int i = 0; i < _zones.Length; i++)
        {
            if (_zones[i] != null)
            {
                _zoneHUDController.UpdateZoneState(_zones[i]);
            }
        }
    }

    private void UpdateCaptureContext()
    {
        CapturableZone playerZone = ZoneControlRuntimeUtils.FindFirstPlayerZone(_zones);

        if (playerZone != null)
        {
            bool show = playerZone.ActiveCaptureActor.HasValue || playerZone.Ownership == ZoneOwnership.Enemy;
            _zoneHUDController.SetCaptureContext(playerZone, show, playerZone.CaptureProgress01, playerZone.ActiveCaptureActor);
        }
        else
        {
            _zoneHUDController.SetCaptureContext(null, false, 0f, null);
        }
    }

    private void UpdateHoldTimer()
    {
        bool allPlayerOwned = ZoneControlRuntimeUtils.AreAllZonesPlayerOwned(_zones);

        if (!allPlayerOwned)
        {
            _holdTimer.Reset();
            _zoneHUDController.SetHoldTimer(false, _holdTimer.Remaining);
            return;
        }

        _holdTimer.Tick(Time.deltaTime);

        _zoneHUDController.SetHoldTimer(true, _holdTimer.Remaining);

        if (_holdTimer.Remaining <= 0f)
        {
            TriggerVictory();
        }
    }

    private void HandleZoneOwnershipChanged(CapturableZone zone, ZoneOwnership previousOwnership, ZoneOwnership newOwnership)
    {
        _zoneHUDController.UpdateZoneState(zone);

        if (_reinforcementManager != null)
        {
            _reinforcementManager.HandleZoneOwnershipChanged(zone, previousOwnership, newOwnership);
        }
    }

    private void HandleUnderAttackChanged(CapturableZone zone, bool isUnderAttack)
    {
        _zoneHUDController.UpdateZoneState(zone);

        if (!isUnderAttack)
        {
            return;
        }

        string message = $"⚠ Zone {zone.Id} is under attack!";
        _zoneHUDController.ShowToast(message);
    }

    private void HandleZoneProgressChanged(CapturableZone zone, float progress, ZoneCaptureActor? actor)
    {
        if (zone != null && zone.IsPlayerInside)
        {
            bool visible = actor.HasValue || zone.Ownership == ZoneOwnership.Enemy;
            _zoneHUDController.SetCaptureContext(zone, visible, progress, actor);
        }
    }

    private bool HandleExternalPlayerDeath(PlayerHealth player)
    {
        if (!_isInitialized || _isMatchEnded)
        {
            return false;
        }

        TriggerDefeat();
        return true;
    }

    private void TriggerVictory()
    {
        if (_isMatchEnded)
        {
            return;
        }

        _isMatchEnded = true;
        FreezeAllDrones();

        _zoneHUDController.SetCaptureContext(null, false, 0f, null);
        _zoneHUDController.SetHoldTimer(false, 0f);
        _zoneEndScreenUI.ShowVictory();
    }

    private void TriggerDefeat()
    {
        if (_isMatchEnded)
        {
            return;
        }

        _isMatchEnded = true;
        FreezeAllDrones();

        _zoneHUDController.SetCaptureContext(null, false, 0f, null);
        _zoneHUDController.SetHoldTimer(false, 0f);
        _zoneEndScreenUI.ShowDefeat();
    }

    private void FreezeAllDrones()
    {
        IReadOnlyList<ZoneDroneController> drones = _enemyDirector.GetAllDrones();
        for (int i = 0; i < drones.Count; i++)
        {
            ZoneDroneController drone = drones[i];
            if (drone != null)
            {
                drone.SetFrozen(true);
            }
        }
    }

    private void EnsureDependencies()
    {
        if (_routeNetwork == null)
        {
            _routeNetwork = GetComponent<ZoneRouteNetwork>();
            if (_routeNetwork == null)
            {
                _routeNetwork = gameObject.AddComponent<ZoneRouteNetwork>();
            }
        }

        if (_coverGenerator == null)
        {
            _coverGenerator = GetComponent<ZoneCoverGenerator>();
            if (_coverGenerator == null)
            {
                _coverGenerator = gameObject.AddComponent<ZoneCoverGenerator>();
            }
        }

        if (_enemyDirector == null)
        {
            _enemyDirector = GetComponent<ZoneEnemyDirector>();
            if (_enemyDirector == null)
            {
                _enemyDirector = gameObject.AddComponent<ZoneEnemyDirector>();
            }
        }

        if (_reinforcementManager == null)
        {
            _reinforcementManager = GetComponent<ReinforcementManager>();
            if (_reinforcementManager == null)
            {
                _reinforcementManager = gameObject.AddComponent<ReinforcementManager>();
            }
        }

        if (_zoneHUDController == null)
        {
            _zoneHUDController = GetComponent<ZoneHUDController>();
            if (_zoneHUDController == null)
            {
                _zoneHUDController = gameObject.AddComponent<ZoneHUDController>();
            }
        }

        if (_zoneEndScreenUI == null)
        {
            _zoneEndScreenUI = GetComponent<ZoneEndScreenUI>();
            if (_zoneEndScreenUI == null)
            {
                _zoneEndScreenUI = gameObject.AddComponent<ZoneEndScreenUI>();
            }
        }

        if (_zoneRoot == null)
        {
            Transform existing = transform.Find("RuntimeZones");
            if (existing == null)
            {
                GameObject root = new GameObject("RuntimeZones");
                root.transform.SetParent(transform, false);
                _zoneRoot = root.transform;
            }
            else
            {
                _zoneRoot = existing;
            }
        }

        if (_enemyRoot == null)
        {
            Transform existing = transform.Find("RuntimeEnemies");
            if (existing == null)
            {
                GameObject root = new GameObject("RuntimeEnemies");
                root.transform.SetParent(transform, false);
                _enemyRoot = root.transform;
            }
            else
            {
                _enemyRoot = existing;
            }
        }
    }

    private void EnsureRuntimeZoneLayout()
    {
        if (HasValidZoneArray())
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                CapturableZone zone = _zones[i];
                if (zone == null)
                {
                    continue;
                }

                zone.ConfigureRuntime(
                    zone.Id,
                    _zoneRadius,
                    _playerCaptureDuration,
                    _enemyRecaptureDuration,
                    _progressDecayDuration,
                    ZoneOwnership.Enemy);
            }

            if (_zoneRoot == null && _zones.Length > 0 && _zones[0] != null)
            {
                _zoneRoot = _zones[0].transform.parent;
            }

            if (!_routeNetwork.HasZone(ZoneId.Alpha) || !_routeNetwork.HasZone(ZoneId.Bravo) || !_routeNetwork.HasZone(ZoneId.Charlie))
            {
                _routeNetwork.BuildDefaultLayout(
                    GetAuthoredZoneCenterOrDefault(ZoneId.Alpha, _alphaDefaultCenter),
                    GetAuthoredZoneCenterOrDefault(ZoneId.Bravo, _bravoDefaultCenter),
                    GetAuthoredZoneCenterOrDefault(ZoneId.Charlie, _charlieDefaultCenter));
            }

            return;
        }

        Vector3 alpha = _alphaDefaultCenter;
        Vector3 bravo = _bravoDefaultCenter;
        Vector3 charlie = _charlieDefaultCenter;

        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
        if (player != null)
        {
            alpha = player.transform.position + new Vector3(2f, 0f, 10f);
            bravo = alpha + new Vector3(12f, 0f, 14f);
            charlie = alpha + new Vector3(-10f, 0f, 30f);
        }

        alpha = SampleToNavMesh(alpha);
        bravo = SampleToNavMesh(bravo);
        charlie = SampleToNavMesh(charlie);

        _routeNetwork.BuildDefaultLayout(alpha, bravo, charlie);

        _zones = new[]
        {
            CreateRuntimeZone(ZoneId.Alpha, alpha),
            CreateRuntimeZone(ZoneId.Bravo, bravo),
            CreateRuntimeZone(ZoneId.Charlie, charlie)
        };
    }

    private CapturableZone CreateRuntimeZone(ZoneId zoneId, Vector3 center)
    {
        string objectName = $"Zone_{zoneId}";
        Transform existing = _zoneRoot.Find(objectName);

        CapturableZone zone;
        if (existing == null)
        {
            GameObject zoneGo = new GameObject(objectName);
            zoneGo.transform.SetParent(_zoneRoot, false);
            zoneGo.transform.position = center;
            zone = zoneGo.AddComponent<CapturableZone>();
        }
        else
        {
            existing.position = center;
            zone = existing.GetComponent<CapturableZone>();
            if (zone == null)
            {
                zone = existing.gameObject.AddComponent<CapturableZone>();
            }
        }

        zone.ConfigureRuntime(
            zoneId,
            _zoneRadius,
            _playerCaptureDuration,
            _enemyRecaptureDuration,
            _progressDecayDuration,
            ZoneOwnership.Enemy);

        return zone;
    }

    private bool HasValidZoneArray()
    {
        if (_zones == null || _zones.Length != 3)
        {
            return false;
        }

        for (int i = 0; i < _zones.Length; i++)
        {
            if (_zones[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetAuthoredZoneCenterOrDefault(ZoneId zoneId, Vector3 fallback)
    {
        for (int i = 0; i < _zones.Length; i++)
        {
            CapturableZone zone = _zones[i];
            if (zone != null && zone.Id == zoneId)
            {
                return SampleToNavMesh(zone.transform.position);
            }
        }

        return SampleToNavMesh(fallback);
    }

    private static Vector3 SampleToNavMesh(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 30f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return point;
    }

    private void BuildZoneLookup()
    {
        _zoneLookup.Clear();
        Dictionary<ZoneId, CapturableZone> rebuiltLookup = ZoneControlRuntimeUtils.BuildZoneLookup(_zones);
        foreach (KeyValuePair<ZoneId, CapturableZone> pair in rebuiltLookup)
        {
            _zoneLookup[pair.Key] = pair.Value;
        }
    }

    private void SubscribeZoneEvents()
    {
        for (int i = 0; i < _zones.Length; i++)
        {
            CapturableZone zone = _zones[i];
            if (zone == null)
            {
                continue;
            }

            zone.OwnershipChanged += HandleZoneOwnershipChanged;
            zone.UnderAttackChanged += HandleUnderAttackChanged;
            zone.ProgressChanged += HandleZoneProgressChanged;
        }
    }

    private void UnsubscribeZoneEvents()
    {
        if (_zones == null)
        {
            return;
        }

        for (int i = 0; i < _zones.Length; i++)
        {
            CapturableZone zone = _zones[i];
            if (zone == null)
            {
                continue;
            }

            zone.OwnershipChanged -= HandleZoneOwnershipChanged;
            zone.UnderAttackChanged -= HandleUnderAttackChanged;
            zone.ProgressChanged -= HandleZoneProgressChanged;
        }
    }

    private void DisableLegacyEnemiesRoot()
    {
        if (_cachedLegacyEnemiesRoot == null || !_cachedLegacyEnemiesRoot)
        {
            _cachedLegacyEnemiesRoot = GameObject.Find(_legacyEnemiesRootName);
        }

        if (_cachedLegacyEnemiesRoot != null && _cachedLegacyEnemiesRoot.activeSelf)
        {
            _cachedLegacyEnemiesRoot.SetActive(false);
        }
    }
}
