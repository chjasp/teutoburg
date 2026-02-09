using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CapturableZone : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private ZoneId _zoneId = ZoneId.Alpha;
    [SerializeField] private ZoneOwnership _startingOwnership = ZoneOwnership.Enemy;

    [Header("Capture")]
    [SerializeField] private float _captureRadius = 10f;
    [SerializeField] private float _playerCaptureDuration = 8f;
    [SerializeField] private float _enemyRecaptureDuration = 12f;
    [SerializeField] private float _progressDecayDuration = 5f;

    [Header("Detection")]
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private LayerMask _enemyMask = ~0;

    [Header("References")]
    [SerializeField] private SphereCollider _captureTrigger;
    [SerializeField] private ZoneBoundaryRing _boundaryRing;
    [SerializeField] private ZoneBeaconVisual _beaconVisual;
    [SerializeField] private Canvas _progressCanvas;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TextMeshProUGUI _progressLabel;

    public event Action<CapturableZone, ZoneOwnership, ZoneOwnership> OwnershipChanged;
    public event Action<CapturableZone, float, ZoneCaptureActor?> ProgressChanged;
    public event Action<CapturableZone, bool> UnderAttackChanged;

    private readonly Collider[] _enemyOverlapBuffer = new Collider[128];
    private readonly HashSet<EnemyHealth> _enemyDedup = new HashSet<EnemyHealth>();
    private readonly List<PlayerHealth> _playerBuffer = new List<PlayerHealth>(4);

    private Transform _playerTransform;
    private PlayerHealth _playerHealth;

    private ZoneOwnership _currentOwnership;
    private ZoneCaptureActor? _activeCaptureActor;
    private float _ownershipProgress;
    private bool _isUnderAttack;
    private bool _isPlayerInside;
    private int _aliveEnemiesInZone;

    public ZoneId Id => _zoneId;
    public ZoneOwnership Ownership => _currentOwnership;
    public ZoneCaptureActor? ActiveCaptureActor => _activeCaptureActor;
    public float CaptureProgress01 => _ownershipProgress;
    public bool IsUnderAttack => _isUnderAttack;
    public bool IsPlayerInside => _isPlayerInside;
    public int AliveEnemiesInZone => _aliveEnemiesInZone;
    public bool IsContested => _isPlayerInside && _aliveEnemiesInZone > 0;
    public float CaptureRadius => _captureRadius;

    private void Awake()
    {
        _currentOwnership = _startingOwnership;
        _ownershipProgress = _currentOwnership == ZoneOwnership.Player ? 1f : 0f;

        EnsureTrigger();
        EnsureVisualReferences();
        EnsureProgressUI();

        ApplyVisualState();
        UpdateProgressUI(true);
    }

    /// <summary>
    /// Configures runtime zone parameters and resets internal capture state.
    /// </summary>
    public void ConfigureRuntime(
        ZoneId zoneId,
        float captureRadius,
        float playerCaptureDuration,
        float enemyRecaptureDuration,
        float progressDecayDuration,
        ZoneOwnership startingOwnership)
    {
        _zoneId = zoneId;
        _captureRadius = Mathf.Max(0.5f, captureRadius);
        _playerCaptureDuration = Mathf.Max(0.1f, playerCaptureDuration);
        _enemyRecaptureDuration = Mathf.Max(0.1f, enemyRecaptureDuration);
        _progressDecayDuration = Mathf.Max(0.1f, progressDecayDuration);
        _startingOwnership = startingOwnership;

        EnsureTrigger();
        _captureTrigger.radius = _captureRadius;
        _captureTrigger.center = Vector3.up * 0.2f;

        _currentOwnership = _startingOwnership;
        _ownershipProgress = _currentOwnership == ZoneOwnership.Player ? 1f : 0f;
        _activeCaptureActor = null;
        _isUnderAttack = false;

        ApplyVisualState();
        UpdateProgressUI(true);
    }

    private void Update()
    {
        RefreshPlayerPresence();
        RefreshEnemyPresence();

        ZoneCaptureActor? previousActor = _activeCaptureActor;
        float previousProgress = _ownershipProgress;

        _activeCaptureActor = ZoneCaptureRuntimeUtils.ResolveCaptureActor(_currentOwnership, _isPlayerInside, _aliveEnemiesInZone);
        _ownershipProgress = ZoneCaptureRuntimeUtils.TickProgress(
            _ownershipProgress,
            _currentOwnership,
            _activeCaptureActor,
            _aliveEnemiesInZone,
            _playerCaptureDuration,
            _enemyRecaptureDuration,
            _progressDecayDuration,
            Time.deltaTime);

        ZoneOwnership previousOwnership = _currentOwnership;
        if (_currentOwnership == ZoneOwnership.Enemy && _ownershipProgress >= 1f)
        {
            SetOwnershipInternal(ZoneOwnership.Player, true);
        }
        else if (_currentOwnership == ZoneOwnership.Player && _ownershipProgress <= 0f)
        {
            SetOwnershipInternal(ZoneOwnership.Enemy, true);
        }

        bool underAttack = ZoneCaptureRuntimeUtils.IsUnderAttack(_currentOwnership, _activeCaptureActor);
        if (underAttack != _isUnderAttack)
        {
            _isUnderAttack = underAttack;
            UnderAttackChanged?.Invoke(this, _isUnderAttack);
        }

        if (Mathf.Abs(previousProgress - _ownershipProgress) > 0.0001f || previousActor != _activeCaptureActor || previousOwnership != _currentOwnership)
        {
            ProgressChanged?.Invoke(this, _ownershipProgress, _activeCaptureActor);
        }

        ApplyVisualState();
        UpdateProgressUI(false);
    }

    /// <summary>
    /// Sets ownership immediately without timing.
    /// </summary>
    public void SetOwnershipImmediate(ZoneOwnership ownership)
    {
        _ownershipProgress = ownership == ZoneOwnership.Player ? 1f : 0f;
        _activeCaptureActor = null;
        SetOwnershipInternal(ownership, false);
        ApplyVisualState();
        UpdateProgressUI(true);
        ProgressChanged?.Invoke(this, _ownershipProgress, _activeCaptureActor);
    }

    /// <summary>
    /// Returns the world-space center used for capture checks.
    /// </summary>
    public Vector3 GetZoneCenter()
    {
        if (_captureTrigger != null)
        {
            return _captureTrigger.bounds.center;
        }

        return transform.position;
    }

    private void RefreshPlayerPresence()
    {
        ResolvePlayerReference();

        bool insideTrackedPlayer = false;
        if (_playerTransform != null && (_playerHealth == null || !_playerHealth.IsDead))
        {
            insideTrackedPlayer = IsInsideRadius(_playerTransform.position);
        }

        _isPlayerInside = insideTrackedPlayer || IsAnyAlivePlayerInsideZone();
    }

    private void ResolvePlayerReference()
    {
        if (_playerTransform != null && _playerHealth != null && !_playerHealth.IsDead && _playerHealth.gameObject.scene == gameObject.scene)
        {
            return;
        }

        _playerTransform = null;
        _playerHealth = null;

        Scene zoneScene = gameObject.scene;
        _playerBuffer.Clear();
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.IsDead)
            {
                continue;
            }

            if (candidate.gameObject.scene == zoneScene)
            {
                _playerBuffer.Add(candidate);
            }
        }

        if (_playerBuffer.Count == 0)
        {
            for (int i = 0; i < players.Length; i++)
            {
                PlayerHealth candidate = players[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.IsDead)
                {
                    continue;
                }

                _playerBuffer.Add(candidate);
            }
        }

        if (_playerBuffer.Count > 0)
        {
            Vector3 center = GetZoneCenter();
            float bestSq = float.MaxValue;
            PlayerHealth best = null;

            for (int i = 0; i < _playerBuffer.Count; i++)
            {
                PlayerHealth candidate = _playerBuffer[i];
                Vector3 pos = candidate.transform.position;
                float dx = pos.x - center.x;
                float dz = pos.z - center.z;
                float sq = dx * dx + dz * dz;

                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = candidate;
                }
            }

            if (best != null)
            {
                _playerHealth = best;
                _playerTransform = best.transform;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_playerTag))
        {
            return;
        }

        try
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerGo == null)
            {
                return;
            }

            _playerTransform = playerGo.transform;
            _playerHealth = playerGo.GetComponentInParent<PlayerHealth>();
        }
        catch (UnityException)
        {
            // Ignore missing tag setup and rely on PlayerHealth lookup.
        }
    }

    private bool IsAnyAlivePlayerInsideZone()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        Scene zoneScene = gameObject.scene;
        bool foundScenePlayer = false;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || !player.gameObject.activeInHierarchy || player.IsDead)
            {
                continue;
            }

            if (player.gameObject.scene != zoneScene)
            {
                continue;
            }

            foundScenePlayer = true;
            if (IsInsideRadius(player.transform.position))
            {
                return true;
            }
        }

        if (foundScenePlayer)
        {
            return false;
        }

        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || !player.gameObject.activeInHierarchy || player.IsDead)
            {
                continue;
            }

            if (IsInsideRadius(player.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInsideRadius(Vector3 worldPosition)
    {
        Vector3 center = GetZoneCenter();
        float dx = worldPosition.x - center.x;
        float dz = worldPosition.z - center.z;
        float sq = dx * dx + dz * dz;
        return sq <= _captureRadius * _captureRadius;
    }

    private void RefreshEnemyPresence()
    {
        Vector3 center = GetZoneCenter();
        _enemyDedup.Clear();

        int overlapCount = Physics.OverlapSphereNonAlloc(
            center,
            _captureRadius,
            _enemyOverlapBuffer,
            _enemyMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = _enemyOverlapBuffer[i];
            if (hit == null)
            {
                continue;
            }

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            _enemyDedup.Add(enemy);
        }

        _aliveEnemiesInZone = _enemyDedup.Count;
    }

    private void SetOwnershipInternal(ZoneOwnership newOwnership, bool playFlipFeedback)
    {
        if (_currentOwnership == newOwnership)
        {
            return;
        }

        ZoneOwnership oldOwnership = _currentOwnership;
        _currentOwnership = newOwnership;

        if (playFlipFeedback && _beaconVisual != null)
        {
            _beaconVisual.PlayOwnershipFlip(newOwnership);
        }

        OwnershipChanged?.Invoke(this, oldOwnership, _currentOwnership);
    }

    private void ApplyVisualState()
    {
        bool contested = IsContested || _activeCaptureActor.HasValue;

        if (_beaconVisual != null)
        {
            _beaconVisual.ApplyState(_currentOwnership, contested, _isUnderAttack);
        }

        if (_boundaryRing != null)
        {
            Color color;
            if (contested)
            {
                color = new Color(0.56f, 0.56f, 0.56f, 0.95f);
            }
            else
            {
                color = _currentOwnership == ZoneOwnership.Player
                    ? new Color(0.2f, 0.55f, 1f, 0.95f)
                    : new Color(0.95f, 0.2f, 0.2f, 0.95f);
            }

            _boundaryRing.SetColor(color);
            _boundaryRing.SetRadius(_captureRadius);
        }
    }

    private void UpdateProgressUI(bool forceHide)
    {
        if (_progressCanvas == null || _progressFill == null)
        {
            return;
        }

        bool show = !forceHide && (_activeCaptureActor.HasValue || (_isPlayerInside && _currentOwnership == ZoneOwnership.Enemy));
        _progressCanvas.gameObject.SetActive(show);

        if (!show)
        {
            return;
        }

        _progressFill.fillAmount = _ownershipProgress;

        if (_activeCaptureActor == ZoneCaptureActor.Enemy)
        {
            _progressFill.color = new Color(0.9f, 0.28f, 0.28f, 0.95f);
        }
        else
        {
            _progressFill.color = new Color(0.2f, 0.55f, 1f, 0.95f);
        }

        if (_progressLabel != null)
        {
            _progressLabel.text = _zoneId.ToString();
        }
    }

    private void EnsureTrigger()
    {
        if (_captureTrigger == null)
        {
            _captureTrigger = GetComponent<SphereCollider>();
        }

        if (_captureTrigger == null)
        {
            _captureTrigger = gameObject.AddComponent<SphereCollider>();
        }

        _captureTrigger.isTrigger = true;
        _captureTrigger.radius = _captureRadius;
        _captureTrigger.center = Vector3.up * 0.2f;
    }

    private void EnsureVisualReferences()
    {
        if (_boundaryRing == null)
        {
            _boundaryRing = GetComponentInChildren<ZoneBoundaryRing>(true);
        }

        if (_beaconVisual == null)
        {
            _beaconVisual = GetComponentInChildren<ZoneBeaconVisual>(true);
        }

        if (_boundaryRing == null)
        {
            GameObject ringGo = new GameObject("ZoneBoundaryRing");
            ringGo.transform.SetParent(transform, false);
            _boundaryRing = ringGo.AddComponent<ZoneBoundaryRing>();
            _boundaryRing.SetRadius(_captureRadius);
        }

        if (_beaconVisual == null)
        {
            GameObject beaconGo = new GameObject("ZoneBeacon");
            beaconGo.transform.SetParent(transform, false);
            beaconGo.transform.localPosition = Vector3.zero;
            _beaconVisual = beaconGo.AddComponent<ZoneBeaconVisual>();
        }
    }

    private void EnsureProgressUI()
    {
        if (_progressCanvas != null && _progressFill != null)
        {
            return;
        }

        GameObject canvasGo = new GameObject("ZoneProgressCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 3.8f, 0f);

        _progressCanvas = canvasGo.AddComponent<Canvas>();
        _progressCanvas.renderMode = RenderMode.WorldSpace;
        _progressCanvas.worldCamera = Camera.main;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 16f;
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = _progressCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2.7f, 0.7f);

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        _progressFill = fillGo.AddComponent<Image>();
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.color = new Color(0.2f, 0.55f, 1f, 0.95f);

        RectTransform fillRect = _progressFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.03f, 0.15f);
        fillRect.anchorMax = new Vector2(0.97f, 0.65f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(bgGo.transform, false);
        _progressLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _progressLabel.fontSize = 2.4f;
        _progressLabel.alignment = TextAlignmentOptions.Center;
        _progressLabel.text = _zoneId.ToString();
        _progressLabel.color = Color.white;

        RectTransform labelRect = _progressLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.65f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        _progressCanvas.gameObject.SetActive(false);
    }
}
