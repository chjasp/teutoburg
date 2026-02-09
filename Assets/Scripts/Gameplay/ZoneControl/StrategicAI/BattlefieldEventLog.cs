using System;
using System.Collections.Generic;
using Axiom.Core;
using UnityEngine;

[DisallowMultipleComponent]
public class BattlefieldEventLog : MonoBehaviour
{
    private struct BattlefieldEventEntry
    {
        public string Key;
        public float Timestamp;
    }

    [Header("Tuning")]
    [SerializeField] private int _maxEvents = 10;
    [SerializeField] private float _zonePollIntervalSeconds = 0.2f;

    [Header("References")]
    [SerializeField] private ZoneControlGameMode _gameMode;
    [SerializeField] private ReinforcementManager _reinforcementManager;

    private readonly List<BattlefieldEventEntry> _entries = new List<BattlefieldEventEntry>(16);
    private readonly List<CapturableZone> _subscribedZones = new List<CapturableZone>(3);

    private PlayerHealth _playerHealth;
    private ZoneId? _lastPlayerZone;
    private bool _healthBelow30Percent;
    private float _nextZonePollTime;
    private bool _isInitialized;

    /// <summary>
    /// Initializes event subscriptions for strategic context logging.
    /// </summary>
    public void Initialize(ZoneControlGameMode gameMode, ReinforcementManager reinforcementManager)
    {
        if (gameMode != null)
        {
            _gameMode = gameMode;
        }

        if (reinforcementManager != null)
        {
            _reinforcementManager = reinforcementManager;
        }

        AutoResolveReferences();
        UnsubscribeAll();
        SubscribeAll();

        _entries.Clear();
        _lastPlayerZone = ResolveCurrentPlayerZone();
        _healthBelow30Percent = false;
        _nextZonePollTime = Time.time + _zonePollIntervalSeconds;
        _isInitialized = true;
    }

    /// <summary>
    /// Adds an event key to the rolling strategic history.
    /// </summary>
    public void Record(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return;
        }

        _entries.Add(new BattlefieldEventEntry
        {
            Key = NormalizeEventKey(eventKey),
            Timestamp = Time.time
        });

        int max = Mathf.Max(1, _maxEvents);
        while (_entries.Count > max)
        {
            _entries.RemoveAt(0);
        }
    }

    /// <summary>
    /// Returns recent events with time-age suffixes for LLM context.
    /// </summary>
    public IReadOnlyList<string> GetRecentEvents(int maxCount)
    {
        int count = Mathf.Clamp(maxCount, 1, Mathf.Max(1, _maxEvents));
        int start = Mathf.Max(0, _entries.Count - count);

        var result = new List<string>(_entries.Count - start);
        for (int i = start; i < _entries.Count; i++)
        {
            BattlefieldEventEntry entry = _entries[i];
            int ageSeconds = Mathf.Max(0, Mathf.RoundToInt(Time.time - entry.Timestamp));
            result.Add($"{entry.Key}_{ageSeconds}s_ago");
        }

        return result;
    }

    private void Awake()
    {
        AutoResolveReferences();
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            UnsubscribeAll();
            SubscribeAll();
        }
    }

    private void OnDisable()
    {
        UnsubscribeAll();
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (Time.time >= _nextZonePollTime)
        {
            _nextZonePollTime = Time.time + Mathf.Max(0.05f, _zonePollIntervalSeconds);
            PollPlayerZoneTransitions();
        }

        RefreshPlayerHealthBinding();
    }

    private void AutoResolveReferences()
    {
        if (_gameMode == null)
        {
            _gameMode = FindFirstObjectByType<ZoneControlGameMode>();
        }

        if (_reinforcementManager == null && _gameMode != null)
        {
            _reinforcementManager = _gameMode.GetComponent<ReinforcementManager>();
        }

        if (_reinforcementManager == null)
        {
            _reinforcementManager = FindFirstObjectByType<ReinforcementManager>();
        }
    }

    private void SubscribeAll()
    {
        SubscribeZones();

        if (_reinforcementManager != null)
        {
            _reinforcementManager.StrategicSquadDispatched += HandleStrategicSquadDispatched;
            _reinforcementManager.StrategicSquadDestroyed += HandleStrategicSquadDestroyed;
        }

        PlayerCombatTelemetry.AttackStyleReported += HandleAttackStyleReported;
        RefreshPlayerHealthBinding();
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < _subscribedZones.Count; i++)
        {
            CapturableZone zone = _subscribedZones[i];
            if (zone == null)
            {
                continue;
            }

            zone.OwnershipChanged -= HandleZoneOwnershipChanged;
        }

        _subscribedZones.Clear();

        if (_reinforcementManager != null)
        {
            _reinforcementManager.StrategicSquadDispatched -= HandleStrategicSquadDispatched;
            _reinforcementManager.StrategicSquadDestroyed -= HandleStrategicSquadDestroyed;
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth = null;
        }

        PlayerCombatTelemetry.AttackStyleReported -= HandleAttackStyleReported;
    }

    private void SubscribeZones()
    {
        if (_gameMode == null || _gameMode.Zones == null)
        {
            return;
        }

        for (int i = 0; i < _gameMode.Zones.Count; i++)
        {
            CapturableZone zone = _gameMode.Zones[i];
            if (zone == null)
            {
                continue;
            }

            zone.OwnershipChanged += HandleZoneOwnershipChanged;
            _subscribedZones.Add(zone);
        }
    }

    private void HandleZoneOwnershipChanged(CapturableZone zone, ZoneOwnership previousOwnership, ZoneOwnership newOwnership)
    {
        if (zone == null)
        {
            return;
        }

        string zoneToken = zone.Id.ToString().ToLowerInvariant();

        if (newOwnership == ZoneOwnership.Player)
        {
            Record($"player_captured_{zoneToken}");
            return;
        }

        if (previousOwnership == ZoneOwnership.Player && newOwnership == ZoneOwnership.Enemy)
        {
            Record($"zone_{zoneToken}_recaptured_by_ai");
        }
    }

    private void PollPlayerZoneTransitions()
    {
        ZoneId? currentZone = ResolveCurrentPlayerZone();

        if (_lastPlayerZone.HasValue && (!currentZone.HasValue || currentZone.Value != _lastPlayerZone.Value))
        {
            string previous = _lastPlayerZone.Value.ToString().ToLowerInvariant();
            Record($"player_retreated_from_{previous}");
        }

        if (currentZone.HasValue && (!_lastPlayerZone.HasValue || currentZone.Value != _lastPlayerZone.Value))
        {
            string current = currentZone.Value.ToString().ToLowerInvariant();
            Record($"player_entered_{current}");
        }

        _lastPlayerZone = currentZone;
    }

    private ZoneId? ResolveCurrentPlayerZone()
    {
        if (_gameMode == null || _gameMode.Zones == null)
        {
            return null;
        }

        for (int i = 0; i < _gameMode.Zones.Count; i++)
        {
            CapturableZone zone = _gameMode.Zones[i];
            if (zone != null && zone.IsPlayerInside)
            {
                return zone.Id;
            }
        }

        return null;
    }

    private void RefreshPlayerHealthBinding()
    {
        if (_playerHealth != null && !_playerHealth.IsDead && _playerHealth.gameObject.activeInHierarchy)
        {
            return;
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth = null;
        }

        PlayerHealth candidate = FindFirstObjectByType<PlayerHealth>();
        if (candidate == null || candidate.IsDead)
        {
            return;
        }

        _playerHealth = candidate;
        _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
        HandlePlayerHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
    }

    private void HandlePlayerHealthChanged(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0)
        {
            return;
        }

        float percent = Mathf.Clamp01((float)currentHealth / maxHealth);
        bool below30 = percent <= 0.3f;

        if (below30 && !_healthBelow30Percent)
        {
            Record("player_health_below_30_percent");
        }

        _healthBelow30Percent = below30;
    }

    private void HandleStrategicSquadDispatched(ZoneId targetZone, int squadSize, float etaSeconds)
    {
        string zoneToken = targetZone.ToString().ToLowerInvariant();
        Record($"reinforcement_squad_deployed_to_{zoneToken}");
    }

    private void HandleStrategicSquadDestroyed(ZoneId targetZone)
    {
        Record("reinforcement_squad_destroyed_by_player");
    }

    private void HandleAttackStyleReported(string style)
    {
        string normalized = string.IsNullOrWhiteSpace(style) ? string.Empty : style.Trim().ToLowerInvariant();
        if (normalized == PlayerCombatTelemetry.AttackStyleMelee)
        {
            Record("player_used_melee_attack");
        }
        else if (normalized == PlayerCombatTelemetry.AttackStyleRanged)
        {
            Record("player_used_ranged_attack");
        }
    }

    private static string NormalizeEventKey(string eventKey)
    {
        string normalized = eventKey.Trim().ToLowerInvariant();
        return normalized.Replace(' ', '_');
    }
}
