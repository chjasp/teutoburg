using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class ZoneDroneController : MonoBehaviour
{
    private enum DroneState
    {
        Patrol,
        Alert,
        Engage,
        ReturnToZone,
        RelocateToTargetZone
    }

    private static readonly List<ZoneDroneController> ActiveControllers = new List<ZoneDroneController>();

    [Header("Role")]
    [SerializeField] private DroneRole _role = DroneRole.Basic;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3.4f;
    [SerializeField] private float _turnSpeed = 8f;
    [SerializeField] private float _patrolRadius = 8f;
    [SerializeField] private float _pathRefreshInterval = 0.45f;

    [Header("Combat")]
    [SerializeField] private float _detectionRange = 18f;
    [SerializeField] private float _engageStartRange = 11f;
    [SerializeField] private float _disengageDelay = 1.1f;

    [Header("Tactics")]
    [SerializeField] private float _zoneLeashDistance = 25f;
    [SerializeField] private float _separationRadius = 2.4f;
    [SerializeField] private float _separationStrength = 1.3f;
    [SerializeField] private float _engageSeparationNudge = 0.45f;
    [SerializeField] private float _flankOffsetDistance = 4.5f;

    [Header("Targeting")]
    [SerializeField] private string _playerTag = "Player";

    private CharacterController _controller;
    private EnemyHealth _enemyHealth;
    private HunterDroneAI _hunterDroneAI;
    private SuppressionDroneAI _suppressionDroneAI;
    private DisruptorDroneAI _disruptorDroneAI;

    private MonoBehaviour _activeCombatBrain;
    private Transform _player;
    private PlayerHealth _playerHealth;
    private ZoneRouteNetwork _routeNetwork;

    private ZoneId _homeZoneId;
    private ZoneId _activeZoneId;
    private Vector3 _homeZoneCenter;
    private Vector3 _activeZoneCenter;

    private DroneState _state;
    private bool _isInitialized;
    private bool _hasRelocationOrder;

    private float _nextPathRefreshTime;
    private float _nextPatrolRetargetTime;
    private float _lastPlayerSeenTime;

    private Vector3 _patrolTarget;
    private bool _hasPatrolTarget;
    private Vector3 _flankPoint;
    private bool _hasFlankPoint;

    private float _gravityVelocityY;
    private bool _isFrozen;

    private NavMeshPath _navPath;
    private int _pathCornerIndex = -1;
    private Vector3 _pathGoal;
    private bool _hasPath;

    public DroneRole Role => _role;
    public ZoneId HomeZoneId => _homeZoneId;
    public ZoneId ActiveZoneId => _activeZoneId;
    public bool IsAlive => _enemyHealth != null && !_enemyHealth.IsDead;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _enemyHealth = GetComponent<EnemyHealth>();
        _navPath = new NavMeshPath();

        _hunterDroneAI = GetComponent<HunterDroneAI>();
        _suppressionDroneAI = GetComponent<SuppressionDroneAI>();
        _disruptorDroneAI = GetComponent<DisruptorDroneAI>();

        ResolveCombatBrain();
        SetCombatBrainActive(false);
    }

    private void OnEnable()
    {
        if (!ActiveControllers.Contains(this))
        {
            ActiveControllers.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveControllers.Remove(this);
    }

    /// <summary>
    /// Initializes zone-driven behavior for this drone.
    /// </summary>
    public void Initialize(ZoneRouteNetwork routeNetwork, ZoneId homeZoneId, Vector3 homeCenter, DroneRole role)
    {
        _routeNetwork = routeNetwork;
        _homeZoneId = homeZoneId;
        _activeZoneId = homeZoneId;
        _homeZoneCenter = homeCenter;
        _activeZoneCenter = homeCenter;
        _role = role;

        ApplyRoleTuning(role);

        _state = DroneState.Patrol;
        _hasRelocationOrder = false;
        _hasPatrolTarget = false;
        _hasFlankPoint = false;
        _isInitialized = true;

        SetCombatBrainActive(false);
    }

    /// <summary>
    /// Sends this drone to another zone objective.
    /// </summary>
    public void IssueRelocationOrder(ZoneId targetZoneId)
    {
        if (!_isInitialized)
        {
            return;
        }

        _hasRelocationOrder = true;
        _activeZoneId = targetZoneId;
        _activeZoneCenter = _routeNetwork != null ? _routeNetwork.GetZoneCenter(targetZoneId) : _activeZoneCenter;
        _state = DroneState.RelocateToTargetZone;
        _hasPatrolTarget = false;
        _hasFlankPoint = false;
    }

    /// <summary>
    /// Freezes or unfreezes zone-driven behavior and combat brain toggling.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
        if (_isFrozen)
        {
            SetCombatBrainActive(false);
        }
    }

    private void Update()
    {
        if (_isFrozen || !_isInitialized || !IsAlive)
        {
            return;
        }

        RefreshPlayerReference();

        bool playerValid = _player != null && (_playerHealth == null || !_playerHealth.IsDead);
        float playerDistance = float.MaxValue;
        if (playerValid)
        {
            playerDistance = Vector3.Distance(transform.position, _player.position);
        }

        bool canDetectPlayer = playerValid && playerDistance <= _detectionRange;
        if (canDetectPlayer)
        {
            _lastPlayerSeenTime = Time.time;
        }

        if (_hasRelocationOrder && _routeNetwork != null)
        {
            _activeZoneCenter = _routeNetwork.GetZoneCenter(_activeZoneId);
        }

        if (_state != DroneState.RelocateToTargetZone && DistanceToActiveZoneCenter() > _zoneLeashDistance && _state != DroneState.ReturnToZone)
        {
            _state = DroneState.ReturnToZone;
            _hasPatrolTarget = false;
            _hasFlankPoint = false;
        }

        switch (_state)
        {
            case DroneState.Patrol:
                TickPatrol(canDetectPlayer);
                break;
            case DroneState.Alert:
                TickAlert(canDetectPlayer, playerDistance);
                break;
            case DroneState.Engage:
                TickEngage(canDetectPlayer);
                break;
            case DroneState.ReturnToZone:
                TickReturnToZone(canDetectPlayer);
                break;
            case DroneState.RelocateToTargetZone:
                TickRelocation(canDetectPlayer, playerDistance);
                break;
        }
    }

    private void LateUpdate()
    {
        if (!IsAlive || _state != DroneState.Engage || _controller == null || !_controller.enabled)
        {
            return;
        }

        Vector3 separation = ComputeSeparationVector() * _engageSeparationNudge;
        if (separation.sqrMagnitude > 0.0001f)
        {
            _controller.Move(separation * Time.deltaTime);
        }
    }

    private void TickPatrol(bool canDetectPlayer)
    {
        SetCombatBrainActive(false);

        if (_hasRelocationOrder)
        {
            _state = DroneState.RelocateToTargetZone;
            _hasPatrolTarget = false;
            return;
        }

        if (canDetectPlayer)
        {
            _state = DroneState.Alert;
            _hasFlankPoint = false;
            return;
        }

        if (!_hasPatrolTarget || Time.time >= _nextPatrolRetargetTime || ReachedPoint(_patrolTarget, 1.25f))
        {
            _patrolTarget = PickPatrolPoint();
            _hasPatrolTarget = true;
            _nextPatrolRetargetTime = Time.time + Random.Range(2.1f, 3.6f);
        }

        MoveTowards(_patrolTarget, 1f);
    }

    private void TickAlert(bool canDetectPlayer, float playerDistance)
    {
        SetCombatBrainActive(false);

        if (_hasRelocationOrder)
        {
            _state = DroneState.RelocateToTargetZone;
            _hasFlankPoint = false;
            return;
        }

        if (!canDetectPlayer)
        {
            _state = DroneState.ReturnToZone;
            return;
        }

        if (_player == null)
        {
            _state = DroneState.ReturnToZone;
            return;
        }

        if (playerDistance <= _engageStartRange)
        {
            _state = DroneState.Engage;
            SetCombatBrainActive(true);
            return;
        }

        Vector3 approach = GetApproachPointToPlayer();
        MoveTowards(approach, 1.12f);
    }

    private void TickEngage(bool canDetectPlayer)
    {
        SetCombatBrainActive(true);

        if (DistanceToActiveZoneCenter() > _zoneLeashDistance)
        {
            _state = DroneState.ReturnToZone;
            SetCombatBrainActive(false);
            return;
        }

        if (!canDetectPlayer && Time.time - _lastPlayerSeenTime >= _disengageDelay)
        {
            _state = DroneState.ReturnToZone;
            SetCombatBrainActive(false);
        }
    }

    private void TickReturnToZone(bool canDetectPlayer)
    {
        SetCombatBrainActive(false);

        if (canDetectPlayer)
        {
            _state = DroneState.Alert;
            return;
        }

        MoveTowards(_activeZoneCenter, 1.03f);
        if (ReachedPoint(_activeZoneCenter, 2.15f))
        {
            _state = DroneState.Patrol;
            _hasPatrolTarget = false;
            _hasFlankPoint = false;
        }
    }

    private void TickRelocation(bool canDetectPlayer, float playerDistance)
    {
        SetCombatBrainActive(false);

        if (canDetectPlayer && playerDistance <= _detectionRange * 0.9f)
        {
            _state = DroneState.Engage;
            SetCombatBrainActive(true);
            return;
        }

        MoveTowards(_activeZoneCenter, 1.1f);
        if (ReachedPoint(_activeZoneCenter, 2.4f))
        {
            _hasRelocationOrder = false;
            _state = DroneState.Patrol;
            _hasPatrolTarget = false;
            _hasFlankPoint = false;
        }
    }

    private void MoveTowards(Vector3 worldTarget, float speedMultiplier)
    {
        if (_controller == null || !_controller.enabled)
        {
            return;
        }

        Vector3 nextPoint = ResolvePathCorner(worldTarget);
        Vector3 toTarget = nextPoint - transform.position;
        toTarget.y = 0f;

        Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
        direction += ComputeSeparationVector();

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, _turnSpeed * Time.deltaTime);
        }

        _gravityVelocityY += Physics.gravity.y * Time.deltaTime;

        Vector3 velocity = direction * (_moveSpeed * speedMultiplier);
        Vector3 motion = new Vector3(velocity.x, _gravityVelocityY, velocity.z) * Time.deltaTime;
        CollisionFlags flags = _controller.Move(motion);

        if ((flags & CollisionFlags.Below) != 0 && _gravityVelocityY < 0f)
        {
            _gravityVelocityY = -0.5f;
        }

        AdvancePathIfNeeded(nextPoint);
    }

    private Vector3 ResolvePathCorner(Vector3 worldTarget)
    {
        if (_navPath == null)
        {
            _navPath = new NavMeshPath();
        }

        if (Time.time >= _nextPathRefreshTime || !_hasPath || (_pathGoal - worldTarget).sqrMagnitude > 0.5f)
        {
            _nextPathRefreshTime = Time.time + _pathRefreshInterval;
            _pathGoal = worldTarget;

            _hasPath = _navPath != null && NavMesh.CalculatePath(transform.position, worldTarget, NavMesh.AllAreas, _navPath);
            _pathCornerIndex = (_hasPath && _navPath.corners != null && _navPath.corners.Length > 1) ? 1 : -1;
        }

        if (_hasPath && _pathCornerIndex >= 0 && _pathCornerIndex < _navPath.corners.Length)
        {
            return _navPath.corners[_pathCornerIndex];
        }

        return worldTarget;
    }

    private void AdvancePathIfNeeded(Vector3 corner)
    {
        if (!_hasPath || _pathCornerIndex < 0)
        {
            return;
        }

        Vector3 toCorner = corner - transform.position;
        toCorner.y = 0f;
        if (toCorner.sqrMagnitude <= 1.4f)
        {
            _pathCornerIndex++;
            if (_pathCornerIndex >= _navPath.corners.Length)
            {
                _pathCornerIndex = -1;
            }
        }
    }

    private Vector3 ComputeSeparationVector()
    {
        Vector3 separation = Vector3.zero;
        int neighbors = 0;

        for (int i = 0; i < ActiveControllers.Count; i++)
        {
            ZoneDroneController other = ActiveControllers[i];
            if (other == null || other == this || !other.IsAlive)
            {
                continue;
            }

            Vector3 delta = transform.position - other.transform.position;
            delta.y = 0f;

            float dist = delta.magnitude;
            if (dist <= 0.001f || dist > _separationRadius)
            {
                continue;
            }

            float strength = 1f - (dist / _separationRadius);
            separation += delta.normalized * strength;
            neighbors++;
        }

        if (neighbors <= 0)
        {
            return Vector3.zero;
        }

        separation /= neighbors;
        return separation * _separationStrength;
    }

    private Vector3 PickPatrolPoint()
    {
        Vector2 offset = Random.insideUnitCircle * _patrolRadius;
        Vector3 point = _activeZoneCenter + new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return point;
    }

    private Vector3 GetApproachPointToPlayer()
    {
        if (_player == null)
        {
            return _activeZoneCenter;
        }

        if (_role != DroneRole.Flanker)
        {
            return _player.position;
        }

        if (!_hasFlankPoint || ReachedPoint(_flankPoint, 1.2f))
        {
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.001f)
            {
                toPlayer = transform.forward;
            }

            toPlayer.Normalize();
            Vector3 side = new Vector3(-toPlayer.z, 0f, toPlayer.x);
            float sideSign = Random.value < 0.5f ? -1f : 1f;
            _flankPoint = _player.position + side * (_flankOffsetDistance * sideSign);
            _hasFlankPoint = true;
        }

        return _flankPoint;
    }

    private bool ReachedPoint(Vector3 point, float distance)
    {
        Vector3 delta = point - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= distance * distance;
    }

    private float DistanceToActiveZoneCenter()
    {
        Vector3 delta = transform.position - _activeZoneCenter;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void ResolveCombatBrain()
    {
        if (_hunterDroneAI != null)
        {
            _activeCombatBrain = _hunterDroneAI;
        }

        if (_suppressionDroneAI != null)
        {
            _activeCombatBrain = _suppressionDroneAI;
        }

        if (_disruptorDroneAI != null)
        {
            _activeCombatBrain = _disruptorDroneAI;
        }
    }

    private void SetCombatBrainActive(bool active)
    {
        if (_hunterDroneAI != null)
        {
            _hunterDroneAI.enabled = active && _activeCombatBrain == _hunterDroneAI;
        }

        if (_suppressionDroneAI != null)
        {
            _suppressionDroneAI.enabled = active && _activeCombatBrain == _suppressionDroneAI;
        }

        if (_disruptorDroneAI != null)
        {
            _disruptorDroneAI.enabled = active && _activeCombatBrain == _disruptorDroneAI;
        }
    }

    private void ApplyRoleTuning(DroneRole role)
    {
        switch (role)
        {
            case DroneRole.Basic:
                _moveSpeed = 3.4f;
                _detectionRange = 17.5f;
                _engageStartRange = 9.5f;
                break;
            case DroneRole.Flanker:
                _moveSpeed = 3.8f;
                _detectionRange = 18.5f;
                _engageStartRange = 11.5f;
                break;
            case DroneRole.Heavy:
                _moveSpeed = 2.8f;
                _detectionRange = 20f;
                _engageStartRange = 12f;
                _zoneLeashDistance = Mathf.Max(_zoneLeashDistance, 27f);
                break;
        }
    }

    private void RefreshPlayerReference()
    {
        if (_player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerGo != null)
            {
                _player = playerGo.transform;
                _playerHealth = playerGo.GetComponentInParent<PlayerHealth>();
            }
        }
    }
}
