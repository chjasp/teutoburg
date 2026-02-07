using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ReinforcementManager : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float _delayMinSeconds = 20f;
    [SerializeField] private float _delayMaxSeconds = 30f;

    [Header("Squad Size")]
    [SerializeField] private int _squadMin = 3;
    [SerializeField] private int _squadMax = 4;

    private ZoneControlGameMode _gameMode;
    private ZoneEnemyDirector _enemyDirector;

    private readonly Dictionary<ZoneId, Coroutine> _pendingReinforcements = new Dictionary<ZoneId, Coroutine>();

    /// <summary>
    /// Initializes recapture reinforcement orchestration.
    /// </summary>
    public void Initialize(ZoneControlGameMode gameMode, ZoneEnemyDirector enemyDirector)
    {
        _gameMode = gameMode;
        _enemyDirector = enemyDirector;
    }

    /// <summary>
    /// Handles ownership changes to schedule or cancel reinforcements.
    /// </summary>
    public void HandleZoneOwnershipChanged(CapturableZone zone, ZoneOwnership previousOwnership, ZoneOwnership newOwnership)
    {
        if (zone == null)
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
    /// Cancels all currently scheduled reinforcements.
    /// </summary>
    public void CancelAll()
    {
        foreach (var pair in _pendingReinforcements)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        _pendingReinforcements.Clear();
    }

    private IEnumerator ScheduleReinforcement(ZoneId targetZoneId)
    {
        float delay = Random.Range(_delayMinSeconds, _delayMaxSeconds);
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
            yield break;
        }

        int squadSize = Random.Range(_squadMin, _squadMax + 1);
        _enemyDirector.SpawnReinforcementSquad(originZone.Value, targetZoneId, squadSize);
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
}
