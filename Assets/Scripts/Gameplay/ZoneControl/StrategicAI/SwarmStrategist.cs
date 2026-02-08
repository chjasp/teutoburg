using System;
using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using UnityEngine;

[DisallowMultipleComponent]
public class SwarmStrategist : MonoBehaviour
{
    private const string DefaultHoldReasoning = "All positions stable. Monitoring target movement.";

    [Header("Mode")]
    [SerializeField] private bool _offlineMode = true;

    [Header("Decision Cadence")]
    [SerializeField] private float _decisionIntervalMinSeconds = 10f;
    [SerializeField] private float _decisionIntervalMaxSeconds = 15f;
    [SerializeField] private float _minimumApiCallIntervalSeconds = 10f;

    [Header("Strategic Dispatch")]
    [SerializeField] private float _reinforcementEtaMinSeconds = 15f;
    [SerializeField] private float _reinforcementEtaMaxSeconds = 25f;

    [Header("LLM")]
    [SerializeField] private LLMApiClientConfig _apiConfig = new LLMApiClientConfig();
    [TextArea(10, 26)]
    [SerializeField] private string _systemPrompt =
        "You are the Swarm Commander - the strategic AI controlling a network of drone squads defending 3 zones " +
        "(Alpha, Bravo, Charlie) against a single human attacker in a zone-control battle.\n\n" +
        "Your goal: prevent the human from capturing and holding all 3 zones simultaneously for 30 seconds.\n\n" +
        "You receive a battlefield snapshot and must respond with a single strategic directive in JSON format. " +
        "You can issue one of: reinforce, redistribute, recapture, hold, feint.\n\n" +
        "Rules:\n" +
        "- Use only available resources from the snapshot.\n" +
        "- Reinforcement squads take 15-25 seconds to arrive.\n" +
        "- Exploit player tendencies, apply deception and timing pressure.\n" +
        "- reasoning must be terse military comms and specific.\n" +
        "- Respond with JSON only; no extra text.";

    [Header("References")]
    [SerializeField] private ZoneControlGameMode _gameMode;
    [SerializeField] private ZoneEnemyDirector _enemyDirector;
    [SerializeField] private ReinforcementManager _reinforcementManager;
    [SerializeField] private BattlefieldEventLog _battlefieldEventLog;
    [SerializeField] private InterceptedTransmissionUI _interceptedTransmissionUI;
    [SerializeField] private SwarmStrategistDebugOverlay _debugOverlay;

    private readonly Dictionary<ZoneId, float> _playerCaptureTimestamps = new Dictionary<ZoneId, float>(3);
    private readonly Dictionary<ZoneId, float> _zoneLossTimestamps = new Dictionary<ZoneId, float>(3);

    private LLMApiClient _apiClient;
    private Coroutine _decisionLoopRoutine;
    private StrategicDirective _lastSuccessfulDirective;
    private float _lastApiCallTimestamp = -999f;
    private bool _isInitialized;
    private bool _zoneEventsSubscribed;

    public event Action<string> OnTransmissionIntercepted;

    public string LastSnapshotJson { get; private set; } = string.Empty;
    public string LastRawLlmResponse { get; private set; } = string.Empty;
    public string LastParsedDirectiveJson { get; private set; } = string.Empty;
    public string LastExecutionStatus { get; private set; } = "Strategist idle.";

    /// <summary>
    /// Initializes strategist dependencies and starts the decision loop.
    /// </summary>
    public void Initialize(
        ZoneControlGameMode gameMode,
        ZoneEnemyDirector director,
        ReinforcementManager reinforcementManager,
        BattlefieldEventLog eventLog)
    {
        if (gameMode != null)
        {
            _gameMode = gameMode;
        }

        if (director != null)
        {
            _enemyDirector = director;
        }

        if (reinforcementManager != null)
        {
            _reinforcementManager = reinforcementManager;
        }

        if (eventLog != null)
        {
            _battlefieldEventLog = eventLog;
        }

        AutoResolveReferences();

        if (_gameMode == null || _enemyDirector == null || _reinforcementManager == null)
        {
            LastExecutionStatus = "Strategist initialization failed: missing dependencies.";
            PushDebugState();
            return;
        }

        _reinforcementManager.SetAutomaticOwnershipResponses(false);

        _apiClient = new LLMApiClient(_apiConfig, _systemPrompt);
        PlayerCombatTelemetry.Reset();
        CacheInitialZoneOwnershipTimestamps();

        if (!_zoneEventsSubscribed)
        {
            SubscribeZoneEvents();
        }

        if (_interceptedTransmissionUI != null)
        {
            _interceptedTransmissionUI.Bind(this);
        }
        else
        {
            Debug.LogWarning("[SwarmStrategist] InterceptedTransmissionUI is not assigned. Intercept messages will not be shown.", this);
        }

        if (_debugOverlay != null)
        {
            _debugOverlay.Bind(this);
        }
        else
        {
            Debug.LogWarning("[SwarmStrategist] SwarmStrategistDebugOverlay is not assigned. Strategist debug telemetry will not be visible.", this);
        }

        _isInitialized = true;
        StartDecisionLoop();
    }

    private void Awake()
    {
        AutoResolveReferences();
    }

    private void Start()
    {
        if (!_isInitialized)
        {
            Initialize(_gameMode, _enemyDirector, _reinforcementManager, _battlefieldEventLog);
        }
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            if (_reinforcementManager != null)
            {
                _reinforcementManager.SetAutomaticOwnershipResponses(false);
            }

            if (!_zoneEventsSubscribed)
            {
                SubscribeZoneEvents();
            }

            StartDecisionLoop();
        }
    }

    private void OnDisable()
    {
        StopDecisionLoop();
        UnsubscribeZoneEvents();

        if (_reinforcementManager != null)
        {
            _reinforcementManager.SetAutomaticOwnershipResponses(true);
        }

        if (_interceptedTransmissionUI != null)
        {
            _interceptedTransmissionUI.Unbind(this);
        }

        if (_debugOverlay != null)
        {
            _debugOverlay.Unbind(this);
        }
    }

    private void AutoResolveReferences()
    {
        if (_gameMode == null)
        {
            _gameMode = GetComponent<ZoneControlGameMode>();
        }

        if (_enemyDirector == null)
        {
            _enemyDirector = GetComponent<ZoneEnemyDirector>();
        }

        if (_reinforcementManager == null)
        {
            _reinforcementManager = GetComponent<ReinforcementManager>();
        }

        if (_battlefieldEventLog == null)
        {
            _battlefieldEventLog = GetComponent<BattlefieldEventLog>();
        }
    }

    private void StartDecisionLoop()
    {
        if (_decisionLoopRoutine != null)
        {
            return;
        }

        _decisionLoopRoutine = StartCoroutine(DecisionLoopRoutine());
    }

    private void StopDecisionLoop()
    {
        if (_decisionLoopRoutine == null)
        {
            return;
        }

        StopCoroutine(_decisionLoopRoutine);
        _decisionLoopRoutine = null;
    }

    private IEnumerator DecisionLoopRoutine()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.8f, 1.6f));

        while (enabled)
        {
            if (_gameMode != null && _gameMode.IsMatchEnded)
            {
                yield return null;
                continue;
            }

            float minInterval = Mathf.Max(1f, _decisionIntervalMinSeconds);
            float maxInterval = Mathf.Max(minInterval, _decisionIntervalMaxSeconds);
            yield return new WaitForSeconds(UnityEngine.Random.Range(minInterval, maxInterval));

            yield return ExecuteDecisionCycle();
        }
    }

    private IEnumerator ExecuteDecisionCycle()
    {
        if (_gameMode == null || _enemyDirector == null || _reinforcementManager == null)
        {
            LastExecutionStatus = "Strategist decision skipped: dependencies missing.";
            PushDebugState();
            yield break;
        }

        BattlefieldSnapshot snapshot = BuildSnapshot();
        LastSnapshotJson = snapshot.ToJson();

        StrategicDirective finalDirective;
        string flowStatus;

        if (_offlineMode)
        {
            finalDirective = BuildOfflineDirective(snapshot, out flowStatus);
            LastRawLlmResponse = "<offline_mode>";
            LastParsedDirectiveJson = finalDirective.ToDebugJson();
            ExecuteDirective(finalDirective, out string executionStatus);
            LastExecutionStatus = $"{flowStatus} {executionStatus}";
            EmitTransmission(finalDirective.reasoning);
            PushDebugState();
            yield break;
        }

        float secondsSinceLastApiCall = Time.time - _lastApiCallTimestamp;
        if (secondsSinceLastApiCall < Mathf.Max(0.1f, _minimumApiCallIntervalSeconds))
        {
            finalDirective = ResolveFallbackDirective(snapshot, "Rate-limited; API call skipped.", out flowStatus);
            LastRawLlmResponse = "<rate_limited>";
            LastParsedDirectiveJson = finalDirective.ToDebugJson();
            ExecuteDirective(finalDirective, out string executionStatus);
            LastExecutionStatus = $"{flowStatus} {executionStatus}";
            EmitTransmission(finalDirective.reasoning);
            PushDebugState();
            yield break;
        }

        _lastApiCallTimestamp = Time.time;

        LLMApiResult apiResult = null;
        yield return _apiClient.RequestDirective(snapshot, r => apiResult = r);

        if (apiResult == null)
        {
            finalDirective = ResolveFallbackDirective(snapshot, "No API result returned.", out flowStatus);
            LastRawLlmResponse = "<no_result>";
            LastParsedDirectiveJson = finalDirective.ToDebugJson();
            ExecuteDirective(finalDirective, out string executionStatus);
            LastExecutionStatus = $"{flowStatus} {executionStatus}";
            EmitTransmission(finalDirective.reasoning);
            PushDebugState();
            yield break;
        }

        LastRawLlmResponse = string.IsNullOrWhiteSpace(apiResult.RawDirectiveText)
            ? apiResult.RawResponseJson
            : apiResult.RawDirectiveText;

        if (!apiResult.Success)
        {
            finalDirective = ResolveFallbackDirective(snapshot, $"API failure: {apiResult.ErrorMessage}", out flowStatus);
            LastParsedDirectiveJson = finalDirective.ToDebugJson();
            ExecuteDirective(finalDirective, out string executionStatus);
            LastExecutionStatus = $"{flowStatus} {executionStatus}";
            EmitTransmission(finalDirective.reasoning);
            PushDebugState();
            yield break;
        }

        if (!StrategicDirectiveParser.TryParse(apiResult.RawDirectiveText, out StrategicDirective parsedDirective, out string parseError))
        {
            Debug.LogWarning(
                $"[SwarmStrategist] Directive parse failed: {parseError}\nRaw LLM response:\n{BuildLogSnippet(apiResult.RawDirectiveText)}",
                this);

            finalDirective = ResolveFallbackDirective(snapshot, $"Directive parse failed: {parseError}", out flowStatus);
            LastParsedDirectiveJson = finalDirective.ToDebugJson();
            ExecuteDirective(finalDirective, out string executionStatus);
            LastExecutionStatus = $"{flowStatus} {executionStatus}";
            EmitTransmission(finalDirective.reasoning);
            PushDebugState();
            yield break;
        }

        DirectiveValidationResult validation = StrategicDirectiveValidator.Validate(parsedDirective, snapshot);
        if (!validation.IsValid)
        {
            Debug.LogWarning(
                $"[SwarmStrategist] Directive validation failed: {validation.Status}\nRaw LLM response:\n{BuildLogSnippet(apiResult.RawDirectiveText)}",
                this);

            finalDirective = ResolveFallbackDirective(snapshot, $"Directive invalid: {validation.Status}", out flowStatus);
            LastParsedDirectiveJson = finalDirective.ToDebugJson();
            ExecuteDirective(finalDirective, out string executionStatus);
            LastExecutionStatus = $"{flowStatus} {executionStatus}";
            EmitTransmission(finalDirective.reasoning);
            PushDebugState();
            yield break;
        }

        finalDirective = validation.Directive;
        _lastSuccessfulDirective = CloneDirective(finalDirective);

        LastParsedDirectiveJson = finalDirective.ToDebugJson();
        ExecuteDirective(finalDirective, out string successfulExecutionStatus);
        LastExecutionStatus = $"LLM directive accepted. {successfulExecutionStatus}";
        EmitTransmission(finalDirective.reasoning);
        PushDebugState();
    }

    private BattlefieldSnapshot BuildSnapshot()
    {
        var snapshot = new BattlefieldSnapshot();

        IReadOnlyList<CapturableZone> zones = _gameMode.Zones;
        if (zones != null)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                CapturableZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                var zoneSnapshot = new ZoneSnapshot
                {
                    id = zone.Id.ToString().ToLowerInvariant(),
                    owner = ResolveZoneOwnerToken(zone),
                    defenders_count = Mathf.Max(0, zone.AliveEnemiesInZone),
                    capture_progress = Mathf.Clamp01(zone.CaptureProgress01)
                };

                if (zone.Ownership == ZoneOwnership.Player && _playerCaptureTimestamps.TryGetValue(zone.Id, out float capturedAtTime))
                {
                    zoneSnapshot.has_seconds_since_captured = true;
                    zoneSnapshot.seconds_since_captured = Mathf.Max(0f, Time.time - capturedAtTime);
                }

                snapshot.zones.Add(zoneSnapshot);
            }
        }

        PopulatePlayerSnapshot(snapshot.player, zones);

        snapshot.ai_resources.total_drones_alive = _enemyDirector.GetAliveDroneCount();
        snapshot.ai_resources.reinforcement_squads_available = _reinforcementManager.StrategicSquadsRemaining;
        snapshot.ai_resources.reinforcement_cooldown_seconds = _reinforcementManager.StrategicCooldownRemainingSeconds;

        snapshot.match_time_seconds = _gameMode.MatchTimeSeconds;

        if (_battlefieldEventLog != null)
        {
            IReadOnlyList<string> recent = _battlefieldEventLog.GetRecentEvents(10);
            for (int i = 0; i < recent.Count; i++)
            {
                snapshot.recent_events.Add(recent[i]);
            }
        }

        return snapshot;
    }

    private void PopulatePlayerSnapshot(PlayerSnapshot playerSnapshot, IReadOnlyList<CapturableZone> zones)
    {
        playerSnapshot.current_zone = "none";
        playerSnapshot.health_percent = 1f;
        playerSnapshot.last_attack_style = PlayerCombatTelemetry.AttackStyleNone;
        playerSnapshot.zones_captured_count = 0;

        if (zones != null)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                CapturableZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                if (zone.Ownership == ZoneOwnership.Player)
                {
                    playerSnapshot.zones_captured_count++;
                }

                if (zone.IsPlayerInside)
                {
                    playerSnapshot.current_zone = zone.Id.ToString();
                }
            }
        }

        if (playerSnapshot.current_zone == "none")
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerSnapshot.current_zone = "transit";
            }
        }

        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
        if (player != null && player.MaxHealth > 0)
        {
            playerSnapshot.health_percent = Mathf.Clamp01((float)player.CurrentHealth / player.MaxHealth);
        }

        playerSnapshot.last_attack_style = string.IsNullOrWhiteSpace(PlayerCombatTelemetry.LastAttackStyle)
            ? PlayerCombatTelemetry.AttackStyleNone
            : PlayerCombatTelemetry.LastAttackStyle;
    }

    private StrategicDirective ResolveFallbackDirective(BattlefieldSnapshot snapshot, string failureReason, out string status)
    {
        if (_lastSuccessfulDirective != null)
        {
            StrategicDirective cached = CloneDirective(_lastSuccessfulDirective);
            DirectiveValidationResult cachedValidation = StrategicDirectiveValidator.Validate(cached, snapshot);
            if (cachedValidation.IsValid)
            {
                StrategicDirective directive = cachedValidation.Directive;
                directive.reasoning = string.IsNullOrWhiteSpace(directive.reasoning)
                    ? DefaultHoldReasoning
                    : directive.reasoning;
                status = $"Fallback to cached directive. Cause: {failureReason}";
                return directive;
            }
        }

        StrategicDirective failHeuristic = BuildApiFailureHeuristicDirective(snapshot);
        DirectiveValidationResult failValidation = StrategicDirectiveValidator.Validate(failHeuristic, snapshot);
        if (failValidation.IsValid)
        {
            status = $"Fallback to API-failure heuristic. Cause: {failureReason}";
            return failValidation.Directive;
        }

        StrategicDirective offlineDirective = BuildOfflineDirective(snapshot, out string offlineStatus);
        status = $"Fallback to offline heuristic ({offlineStatus}). Cause: {failureReason}";
        return offlineDirective;
    }

    private StrategicDirective BuildApiFailureHeuristicDirective(BattlefieldSnapshot snapshot)
    {
        if (TryGetMostRecentlyLostPlayerZone(snapshot, out ZoneId lostZone, out _))
        {
            return new StrategicDirective
            {
                order = "recapture",
                target_zone = lostZone.ToString(),
                squad_size = 3,
                reasoning = $"Signal degraded. Reclaiming {lostZone} to stabilize perimeter."
            };
        }

        if (TryBuildRedistributeTowardPlayer(snapshot, out StrategicDirective redistributeDirective))
        {
            return redistributeDirective;
        }

        return StrategicDirective.CreateHold("Comms degraded. Holding sectors and observing target movement.");
    }

    private StrategicDirective BuildOfflineDirective(BattlefieldSnapshot snapshot, out string status)
    {
        if (TryGetMostRecentlyLostPlayerZone(snapshot, out ZoneId recentlyLostZone, out float elapsedSeconds) && elapsedSeconds >= 20f)
        {
            status = "Offline heuristic: recapture recently lost zone.";
            return new StrategicDirective
            {
                order = "recapture",
                target_zone = recentlyLostZone.ToString(),
                squad_size = 4,
                reasoning = $"{recentlyLostZone} lost {Mathf.RoundToInt(elapsedSeconds)}s ago. Executing timed recapture push."
            };
        }

        if (StrategicDirective.TryParseZoneId(snapshot.player.current_zone, out ZoneId playerZone))
        {
            ZoneSnapshot zoneSnapshot = snapshot.FindZone(playerZone);
            if (zoneSnapshot != null && zoneSnapshot.defenders_count <= 2)
            {
                status = "Offline heuristic: reinforce player current zone with low defenders.";
                return new StrategicDirective
                {
                    order = "reinforce",
                    target_zone = playerZone.ToString(),
                    squad_size = 3,
                    reasoning = $"Target inside {playerZone}. Defender screen thin. Reinforcing immediately."
                };
            }

            if (snapshot.player.health_percent <= 0.35f)
            {
                status = "Offline heuristic: aggressive push on low-health player.";
                return new StrategicDirective
                {
                    order = "reinforce",
                    target_zone = playerZone.ToString(),
                    squad_size = 4,
                    reasoning = $"Target integrity low at {Mathf.RoundToInt(snapshot.player.health_percent * 100f)}%. Converging for finish."
                };
            }
        }

        status = "Offline heuristic: hold.";
        return StrategicDirective.CreateHold("All sectors stable. Shadowing target movement.");
    }

    private void ExecuteDirective(StrategicDirective directive, out string executionStatus)
    {
        if (directive == null)
        {
            executionStatus = "No directive to execute.";
            return;
        }

        directive.Normalize();
        if (string.IsNullOrWhiteSpace(directive.reasoning))
        {
            directive.reasoning = DefaultHoldReasoning;
        }

        switch (directive.GetOrderType())
        {
            case StrategicOrderType.Reinforce:
            case StrategicOrderType.Recapture:
                ExecuteReinforceLike(directive, out executionStatus);
                return;

            case StrategicOrderType.Redistribute:
                ExecuteRedistribute(directive, out executionStatus);
                return;

            case StrategicOrderType.Feint:
                ExecuteFeint(directive, out executionStatus);
                return;

            case StrategicOrderType.Hold:
                executionStatus = "Directive hold executed: no movement orders issued.";
                return;

            default:
                executionStatus = $"Unknown directive order '{directive.order}'.";
                return;
        }
    }

    private void ExecuteReinforceLike(StrategicDirective directive, out string status)
    {
        if (!StrategicDirective.TryParseZoneId(directive.target_zone, out ZoneId targetZone))
        {
            status = "Reinforce/recapture failed: invalid target zone.";
            return;
        }

        int squadSize = Mathf.Clamp(directive.squad_size, 1, 8);
        bool dispatched = _reinforcementManager.TryDispatchStrategicSquad(
            targetZone,
            squadSize,
            _reinforcementEtaMinSeconds,
            _reinforcementEtaMaxSeconds,
            out float eta,
            out string dispatchStatus);

        status = dispatched
            ? $"{directive.order} executed: squad {squadSize} -> {targetZone}, ETA {Mathf.RoundToInt(eta)}s."
            : $"{directive.order} not executed: {dispatchStatus}";
    }

    private void ExecuteRedistribute(StrategicDirective directive, out string status)
    {
        if (!StrategicDirective.TryParseZoneId(directive.from_zone, out ZoneId fromZone) ||
            !StrategicDirective.TryParseZoneId(directive.to_zone, out ZoneId toZone))
        {
            status = "redistribute failed: invalid from/to zones.";
            return;
        }

        int requested = Mathf.Max(1, directive.count);
        int moved = _enemyDirector.RedistributeDefenders(fromZone, toZone, requested);
        status = moved > 0
            ? $"redistribute executed: moved {moved}/{requested} from {fromZone} to {toZone}."
            : "redistribute executed with no available defenders to move.";
    }

    private void ExecuteFeint(StrategicDirective directive, out string status)
    {
        if (!StrategicDirective.TryParseZoneId(directive.decoy_zone, out ZoneId decoyZone) ||
            !StrategicDirective.TryParseZoneId(directive.real_target_zone, out ZoneId realTargetZone))
        {
            status = "feint failed: invalid decoy or real target zone.";
            return;
        }

        int decoySize = Mathf.Clamp(directive.decoy_size, 1, 8);
        int realSize = Mathf.Clamp(directive.real_size, 1, 8);

        bool decoyDispatched = _reinforcementManager.TryDispatchStrategicSquad(
            decoyZone,
            decoySize,
            _reinforcementEtaMinSeconds,
            _reinforcementEtaMaxSeconds,
            out float decoyEta,
            out string decoyStatus);

        bool realDispatched = _reinforcementManager.TryDispatchStrategicSquad(
            realTargetZone,
            realSize,
            _reinforcementEtaMinSeconds,
            _reinforcementEtaMaxSeconds,
            out float realEta,
            out string realStatus);

        status =
            $"feint decoy: {(decoyDispatched ? $"OK -> {decoyZone} ETA {Mathf.RoundToInt(decoyEta)}s" : decoyStatus)}; " +
            $"real: {(realDispatched ? $"OK -> {realTargetZone} ETA {Mathf.RoundToInt(realEta)}s" : realStatus)}";
    }

    private void EmitTransmission(string message)
    {
        string transmission = string.IsNullOrWhiteSpace(message)
            ? DefaultHoldReasoning
            : message.Trim();

        OnTransmissionIntercepted?.Invoke(transmission);
    }

    private void PushDebugState()
    {
        if (_debugOverlay != null)
        {
            _debugOverlay.SetState(LastSnapshotJson, LastRawLlmResponse, LastParsedDirectiveJson, LastExecutionStatus);
        }
    }

    private void CacheInitialZoneOwnershipTimestamps()
    {
        _playerCaptureTimestamps.Clear();
        _zoneLossTimestamps.Clear();

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

            if (zone.Ownership == ZoneOwnership.Player)
            {
                _playerCaptureTimestamps[zone.Id] = Time.time;
                _zoneLossTimestamps[zone.Id] = Time.time;
            }
        }
    }

    private void SubscribeZoneEvents()
    {
        if (_gameMode == null || _gameMode.Zones == null)
        {
            return;
        }

        for (int i = 0; i < _gameMode.Zones.Count; i++)
        {
            CapturableZone zone = _gameMode.Zones[i];
            if (zone != null)
            {
                zone.OwnershipChanged += HandleZoneOwnershipChanged;
            }
        }

        _zoneEventsSubscribed = true;
    }

    private void UnsubscribeZoneEvents()
    {
        if (!_zoneEventsSubscribed || _gameMode == null || _gameMode.Zones == null)
        {
            return;
        }

        for (int i = 0; i < _gameMode.Zones.Count; i++)
        {
            CapturableZone zone = _gameMode.Zones[i];
            if (zone != null)
            {
                zone.OwnershipChanged -= HandleZoneOwnershipChanged;
            }
        }

        _zoneEventsSubscribed = false;
    }

    private void HandleZoneOwnershipChanged(CapturableZone zone, ZoneOwnership previousOwnership, ZoneOwnership newOwnership)
    {
        if (zone == null)
        {
            return;
        }

        if (newOwnership == ZoneOwnership.Player)
        {
            _playerCaptureTimestamps[zone.Id] = Time.time;
            _zoneLossTimestamps[zone.Id] = Time.time;
            return;
        }

        _playerCaptureTimestamps.Remove(zone.Id);
        _zoneLossTimestamps.Remove(zone.Id);
    }

    private bool TryGetMostRecentlyLostPlayerZone(BattlefieldSnapshot snapshot, out ZoneId zoneId, out float elapsedSeconds)
    {
        zoneId = ZoneId.Alpha;
        elapsedSeconds = 0f;

        float newestTimestamp = float.MinValue;
        bool found = false;

        foreach (KeyValuePair<ZoneId, float> pair in _zoneLossTimestamps)
        {
            ZoneSnapshot zoneSnapshot = snapshot.FindZone(pair.Key);
            if (zoneSnapshot == null || !string.Equals(zoneSnapshot.owner, "player", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!found || pair.Value > newestTimestamp)
            {
                newestTimestamp = pair.Value;
                zoneId = pair.Key;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        elapsedSeconds = Mathf.Max(0f, Time.time - newestTimestamp);
        return true;
    }

    private bool TryBuildRedistributeTowardPlayer(BattlefieldSnapshot snapshot, out StrategicDirective directive)
    {
        directive = null;

        if (!StrategicDirective.TryParseZoneId(snapshot.player.current_zone, out ZoneId playerZone))
        {
            return false;
        }

        ZoneId? bestSourceZone = null;
        int maxDefenders = 0;

        for (int i = 0; i < snapshot.zones.Count; i++)
        {
            ZoneSnapshot zone = snapshot.zones[i];
            if (zone == null)
            {
                continue;
            }

            if (!StrategicDirective.TryParseZoneId(zone.id, out ZoneId zoneId))
            {
                continue;
            }

            if (zoneId == playerZone)
            {
                continue;
            }

            if (zone.defenders_count > maxDefenders)
            {
                maxDefenders = zone.defenders_count;
                bestSourceZone = zoneId;
            }
        }

        if (!bestSourceZone.HasValue || maxDefenders <= 1)
        {
            return false;
        }

        int count = Mathf.Clamp(maxDefenders / 2, 1, 4);
        directive = new StrategicDirective
        {
            order = "redistribute",
            from_zone = bestSourceZone.Value.ToString(),
            to_zone = playerZone.ToString(),
            count = count,
            reasoning = $"Target pressure focused on {playerZone}. Sliding defenders from {bestSourceZone.Value}."
        };

        return true;
    }

    private static StrategicDirective CloneDirective(StrategicDirective directive)
    {
        if (directive == null)
        {
            return null;
        }

        return JsonUtility.FromJson<StrategicDirective>(JsonUtility.ToJson(directive));
    }

    private static string BuildLogSnippet(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "<empty>";
        }

        const int maxChars = 2400;
        string trimmed = text.Trim();
        if (trimmed.Length <= maxChars)
        {
            return trimmed;
        }

        return $"{trimmed.Substring(0, maxChars)}\n...[truncated]";
    }

    private static string ResolveZoneOwnerToken(CapturableZone zone)
    {
        if (zone == null)
        {
            return "ai";
        }

        if (zone.IsContested || zone.ActiveCaptureActor.HasValue)
        {
            return "contested";
        }

        return zone.Ownership == ZoneOwnership.Player ? "player" : "ai";
    }
}
