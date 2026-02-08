using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ReinforcementManager : MonoBehaviour
{
    private sealed class StrategicSquadTracker
    {
        public ZoneId TargetZone;
        public List<ZoneDroneController> Drones = new List<ZoneDroneController>(8);
    }

    [Header("Automatic Ownership Response")]
    [SerializeField] private bool _automaticOwnershipResponsesEnabled = true;

    [Header("Timing")]
    [SerializeField] private float _delayMinSeconds = 20f;
    [SerializeField] private float _delayMaxSeconds = 30f;

    [Header("Squad Size")]
    [SerializeField] private int _squadMin = 3;
    [SerializeField] private int _squadMax = 4;

    [Header("Strategic Dispatch")]
    [SerializeField] private int _strategicSquadsMax = 4;
    [SerializeField] private float _strategicCooldownSeconds = 10f;

    private ZoneControlGameMode _gameMode;
    private ZoneEnemyDirector _enemyDirector;

    private readonly Dictionary<ZoneId, Coroutine> _pendingReinforcements = new Dictionary<ZoneId, Coroutine>();
    private readonly List<Coroutine> _strategicDispatchRoutines = new List<Coroutine>(8);
    private readonly List<StrategicSquadTracker> _activeStrategicSquads = new List<StrategicSquadTracker>(8);

    private int _strategicSquadsRemaining;
    private float _nextStrategicDispatchTime;
    private int _lastDispatchFrame = -1;
    private int _dispatchesIssuedThisFrame;

    public event Action<ZoneId, int, float> StrategicSquadDispatched;
    public event Action<ZoneId> StrategicSquadDestroyed;

    public int StrategicSquadsRemaining => Mathf.Max(0, _strategicSquadsRemaining);
    public float StrategicCooldownRemainingSeconds => Mathf.Max(0f, _nextStrategicDispatchTime - Time.time);

    private void Awake()
    {
        _strategicSquadsRemaining = Mathf.Max(0, _strategicSquadsMax);
    }

    private void OnDisable()
    {
        CancelAll();
    }

    private void Update()
    {
        TrackStrategicSquadLifecycle();
    }

    /// <summary>
    /// Initializes recapture reinforcement orchestration.
    /// </summary>
    public void Initialize(ZoneControlGameMode gameMode, ZoneEnemyDirector enemyDirector)
    {
        _gameMode = gameMode;
        _enemyDirector = enemyDirector;

        _strategicSquadsRemaining = Mathf.Max(0, _strategicSquadsMax);
        _nextStrategicDispatchTime = 0f;
        _lastDispatchFrame = -1;
        _dispatchesIssuedThisFrame = 0;
    }

    /// <summary>
    /// Enables or disables automatic ownership-triggered reinforcement behavior.
    /// </summary>
    public void SetAutomaticOwnershipResponses(bool enabled)
    {
        _automaticOwnershipResponsesEnabled = enabled;

        if (!enabled)
        {
            foreach (ZoneId zoneId in new List<ZoneId>(_pendingReinforcements.Keys))
            {
                CancelPending(zoneId);
            }
        }
    }

    /// <summary>
    /// Handles ownership changes to schedule or cancel reinforcements.
    /// </summary>
    public void HandleZoneOwnershipChanged(CapturableZone zone, ZoneOwnership previousOwnership, ZoneOwnership newOwnership)
    {
        if (!_automaticOwnershipResponsesEnabled || zone == null)
        {
            return;
        }

        ZoneId zoneId = zone.Id;

        if (newOwnership == ZoneOwnership.Player)
        {
            if (!_pendingReinforcements.ContainsKey(zoneId))
            {
                Coroutine routine = StartCoroutine(ScheduleReinforcement(zoneId));
                _pendingReinforcements[zoneId] = routine;
            }
        }
        else
        {
            CancelPending(zoneId);
        }
    }

    /// <summary>
    /// Attempts to dispatch a strategist-controlled squad with travel delay.
    /// </summary>
    public bool TryDispatchStrategicSquad(
        ZoneId targetZone,
        int squadSize,
        float minEta,
        float maxEta,
        out float etaSeconds,
        out string status)
    {
        etaSeconds = 0f;
        status = string.Empty;

        if (_gameMode == null || _enemyDirector == null)
        {
            status = "Strategic dispatch unavailable: manager is not initialized.";
            return false;
        }

        if (_gameMode.IsMatchEnded)
        {
            status = "Strategic dispatch blocked: match has ended.";
            return false;
        }

        if (_strategicSquadsRemaining <= 0)
        {
            status = "Strategic dispatch blocked: no squads remaining.";
            return false;
        }

        if (Time.frameCount != _lastDispatchFrame)
        {
            _lastDispatchFrame = Time.frameCount;
            _dispatchesIssuedThisFrame = 0;
        }

        bool allowChainedDispatchThisFrame = _dispatchesIssuedThisFrame > 0;
        if (!allowChainedDispatchThisFrame && StrategicCooldownRemainingSeconds > 0.05f)
        {
            status = $"Strategic dispatch cooling down ({Mathf.CeilToInt(StrategicCooldownRemainingSeconds)}s).";
            return false;
        }

        ZoneId? originZone = _gameMode.PickRandomEnemyOwnedZone(targetZone);
        if (!originZone.HasValue)
        {
            originZone = FindAnyOriginZone(targetZone);
        }

        if (!originZone.HasValue)
        {
            status = "Strategic dispatch failed: no valid origin zone available.";
            return false;
        }

        int clampedSize = Mathf.Clamp(squadSize, 1, 8);
        float etaMin = Mathf.Max(0.1f, Mathf.Min(minEta, maxEta));
        float etaMax = Mathf.Max(etaMin, Mathf.Max(minEta, maxEta));
        etaSeconds = UnityEngine.Random.Range(etaMin, etaMax);

        Coroutine routine = StartCoroutine(DispatchStrategicAfterDelay(originZone.Value, targetZone, clampedSize, etaSeconds));
        _strategicDispatchRoutines.Add(routine);

        _strategicSquadsRemaining = Mathf.Max(0, _strategicSquadsRemaining - 1);
        _dispatchesIssuedThisFrame++;

        if (!allowChainedDispatchThisFrame)
        {
            _nextStrategicDispatchTime = Time.time + Mathf.Max(0f, _strategicCooldownSeconds);
        }

        status = $"Strategic squad queued ({clampedSize}) from {originZone.Value} to {targetZone}.";
        return true;
    }

    /// <summary>
    /// Cancels all currently scheduled reinforcements.
    /// </summary>
    public void CancelAll()
    {
        foreach (KeyValuePair<ZoneId, Coroutine> pair in _pendingReinforcements)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        _pendingReinforcements.Clear();

        for (int i = 0; i < _strategicDispatchRoutines.Count; i++)
        {
            Coroutine routine = _strategicDispatchRoutines[i];
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        _strategicDispatchRoutines.Clear();
        _activeStrategicSquads.Clear();
    }

    private IEnumerator ScheduleReinforcement(ZoneId targetZoneId)
    {
        float delay = UnityEngine.Random.Range(_delayMinSeconds, _delayMaxSeconds);
        yield return new WaitForSeconds(delay);

        _pendingReinforcements.Remove(targetZoneId);

        if (_gameMode == null || _enemyDirector == null || _gameMode.IsMatchEnded)
        {
            yield break;
        }

        CapturableZone zone = _gameMode.GetZone(targetZoneId);
        if (zone == null || zone.Ownership != ZoneOwnership.Player)
        {
            yield break;
        }

        ZoneId? originZone = _gameMode.PickRandomEnemyOwnedZone(targetZoneId);
        if (!originZone.HasValue)
        {
            originZone = FindAnyOriginZone(targetZoneId);
        }

        if (!originZone.HasValue)
        {
            yield break;
        }

        int squadSize = UnityEngine.Random.Range(_squadMin, _squadMax + 1);
        _enemyDirector.SpawnReinforcementSquad(originZone.Value, targetZoneId, squadSize);
    }

    private IEnumerator DispatchStrategicAfterDelay(ZoneId originZone, ZoneId targetZone, int squadSize, float etaSeconds)
    {
        yield return new WaitForSeconds(etaSeconds);

        if (_gameMode == null || _enemyDirector == null || _gameMode.IsMatchEnded)
        {
            yield break;
        }

        List<ZoneDroneController> squad = _enemyDirector.SpawnReinforcementSquad(originZone, targetZone, squadSize);
        int spawnedCount = squad != null ? squad.Count : 0;

        if (spawnedCount <= 0)
        {
            _strategicSquadsRemaining = Mathf.Min(_strategicSquadsMax, _strategicSquadsRemaining + 1);
            yield break;
        }

        StrategicSquadDispatched?.Invoke(targetZone, spawnedCount, etaSeconds);

        var tracker = new StrategicSquadTracker
        {
            TargetZone = targetZone,
            Drones = squad
        };

        _activeStrategicSquads.Add(tracker);
    }

    private void CancelPending(ZoneId zoneId)
    {
        if (!_pendingReinforcements.TryGetValue(zoneId, out Coroutine routine))
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        _pendingReinforcements.Remove(zoneId);
    }

    private void TrackStrategicSquadLifecycle()
    {
        for (int i = _activeStrategicSquads.Count - 1; i >= 0; i--)
        {
            StrategicSquadTracker tracker = _activeStrategicSquads[i];
            if (tracker == null || tracker.Drones == null || tracker.Drones.Count == 0)
            {
                _activeStrategicSquads.RemoveAt(i);
                continue;
            }

            bool allDestroyed = true;
            for (int j = 0; j < tracker.Drones.Count; j++)
            {
                ZoneDroneController drone = tracker.Drones[j];
                if (drone != null && drone.IsAlive)
                {
                    allDestroyed = false;
                    break;
                }
            }

            if (allDestroyed)
            {
                StrategicSquadDestroyed?.Invoke(tracker.TargetZone);
                _activeStrategicSquads.RemoveAt(i);
            }
        }
    }

    private ZoneId? FindAnyOriginZone(ZoneId targetZone)
    {
        ZoneId[] zones = { ZoneId.Alpha, ZoneId.Bravo, ZoneId.Charlie };
        ZoneId? bestZone = null;
        int maxDrones = 0;

        for (int i = 0; i < zones.Length; i++)
        {
            ZoneId zoneId = zones[i];
            if (zoneId == targetZone)
            {
                continue;
            }

            List<ZoneDroneController> drones = _enemyDirector.GetAliveDronesByActiveZone(zoneId);
            int count = drones != null ? drones.Count : 0;
            if (count > maxDrones)
            {
                maxDrones = count;
                bestZone = zoneId;
            }
        }

        return bestZone;
    }
}
