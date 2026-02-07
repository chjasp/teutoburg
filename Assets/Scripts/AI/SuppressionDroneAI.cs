using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class SuppressionDroneAI : MonoBehaviour, IEnemyAttackTuning, IEnemyAggro, IStunnable
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3.2f;
    [SerializeField] private float _turnSpeed = 8f;
    [SerializeField] private float _preferredDistance = 12f;
    [SerializeField] private float _minDistance = 8f;
    [SerializeField] private float _awarenessRange = 25f;
    [SerializeField] private float _repositionDistance = 6f;
    [SerializeField] private float _repositionCooldown = 1.25f;

    [Header("High Ground (Optional)")]
    [SerializeField] private Transform[] _perchPoints;
    [SerializeField] private float _perchHeightBias = 1.5f;

    [Header("Base Attack - Mortar Lob")]
    [SerializeField] private EnemyMortarProjectile _mortarPrefab;
    [SerializeField] private Transform _mortarMuzzle;
    [SerializeField] private float _mortarInterval = 3f;
    [SerializeField] private int _mortarDamage = 12;
    [SerializeField] private float _mortarFlightTime = 1.2f;
    [SerializeField] private float _mortarArcHeight = 2.5f;
    [SerializeField] private float _mortarExplosionRadius = 3f;

    [Header("Special Attack - Barrage")]
    [SerializeField] private int _barrageCount = 3;
    [SerializeField] private float _barrageShotSpacing = 0.25f;
    [SerializeField] private float _barrageCooldown = 10f;
    [SerializeField, Range(0f, 1f)] private float _barrageRandomChance = 0.2f;
    [SerializeField] private float _barrageRandomCheckInterval = 1.5f;

    [Header("Targeting")]
    [SerializeField] private Transform _player;
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _rescanInterval = 0.5f;
    [SerializeField] private float _leadTime = 0.35f;
    [SerializeField] private float _campingSpeedThreshold = 0.2f;
    [SerializeField] private float _campingTimeThreshold = 1.5f;

    private CharacterController _controller;
    private float _gravityVelocityY;
    private float _scanTimer;
    private Transform _currentTarget;
    private bool _isAggroed;

    private float _attackTimer;
    private bool _isBarrageFiring;
    private Coroutine _barrageRoutine;
    private float _barrageReadyTime;
    private float _nextRandomBarrageCheck;

    private Vector3 _lastTargetPosition;
    private float _lastTargetSampleTime;
    private Vector3 _estimatedTargetVelocity;
    private float _campingTimer;
    private bool _hasTargetSample;

    private Vector3 _repositionTarget;
    private bool _hasRepositionTarget;
    private float _nextRepositionTime;

    private int _baseMortarDamage;
    private float _stunnedUntil;

    void Awake()
    {
        DroneVisualRig visualRig = DroneVisualBuilder.EnsureVisuals(transform, DroneArchetype.Suppression);
        if (_mortarMuzzle == null && visualRig.Muzzle != null)
        {
            _mortarMuzzle = visualRig.Muzzle;
        }

        _controller = GetComponent<CharacterController>();
        _baseMortarDamage = _mortarDamage;

        if (_player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerGo != null) _player = playerGo.transform;
        }
    }

    public int BaseAttackDamage => _baseMortarDamage;

    public void SetAttackDamage(int value)
    {
        _mortarDamage = Mathf.Max(0, value);
    }

    void Update()
    {
        if (IsStunned)
        {
            ApplyGravityOnly();
            return;
        }

        _scanTimer += Time.deltaTime;
        if (_currentTarget == null || _scanTimer >= _rescanInterval)
        {
            _scanTimer = 0f;
            AcquireTarget();
        }

        if (_currentTarget == null)
        {
            ApplyGravityOnly();
            return;
        }

        if (IsTargetDead(_currentTarget))
        {
            _currentTarget = null;
            _isAggroed = false;
            return;
        }

        Vector3 toTarget = _currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (!_isAggroed && distance > _awarenessRange)
        {
            _currentTarget = null;
            return;
        }

        FaceTarget(toTarget);
        UpdateTargetVelocity();

        UpdateMovement(toTarget, distance);
        UpdateAttacks(distance);
    }

    private void ApplyGravityOnly()
    {
        _gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        var flags = _controller.Move(new Vector3(0f, _gravityVelocityY, 0f) * Time.deltaTime);
        if ((flags & CollisionFlags.Below) != 0 && _gravityVelocityY < 0f)
        {
            _gravityVelocityY = -0.5f;
        }
    }

    private void FaceTarget(Vector3 toTarget)
    {
        if (toTarget.sqrMagnitude <= 0.001f) return;
        Quaternion face = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, face, _turnSpeed * Time.deltaTime);
    }

    private void UpdateMovement(Vector3 toTarget, float distance)
    {
        if (_hasRepositionTarget)
        {
            MoveToward(_repositionTarget);
            if (Vector3.Distance(transform.position, _repositionTarget) <= 0.5f)
            {
                _hasRepositionTarget = false;
            }
            return;
        }

        if (distance < _minDistance && Time.time >= _nextRepositionTime)
        {
            ChooseRepositionTarget(toTarget);
            _nextRepositionTime = Time.time + _repositionCooldown;
            if (_hasRepositionTarget)
            {
                MoveToward(_repositionTarget);
                return;
            }
        }

        Vector3 move = Vector3.zero;
        if (distance > _preferredDistance)
        {
            move = toTarget.normalized;
        }
        else if (distance < _minDistance)
        {
            move = -toTarget.normalized;
        }

        MoveDirection(move);
    }

    private void MoveToward(Vector3 worldTarget)
    {
        Vector3 toPoint = worldTarget - transform.position;
        toPoint.y = 0f;
        Vector3 move = toPoint.sqrMagnitude > 0.01f ? toPoint.normalized : Vector3.zero;
        MoveDirection(move);
    }

    private void MoveDirection(Vector3 move)
    {
        _gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(move.x * _moveSpeed, _gravityVelocityY, move.z * _moveSpeed) * Time.deltaTime;
        var flags = _controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && _gravityVelocityY < 0f)
        {
            _gravityVelocityY = -0.5f;
        }
    }

    private void ChooseRepositionTarget(Vector3 toTarget)
    {
        _hasRepositionTarget = false;

        Transform perch = GetBestPerchPoint();
        if (perch != null)
        {
            _repositionTarget = perch.position;
            _hasRepositionTarget = true;
            return;
        }

        Vector3 away = -toTarget.normalized;
        Vector3 strafe = Vector3.Cross(Vector3.up, away).normalized * Random.Range(-1f, 1f);
        Vector3 dir = (away + strafe).normalized;
        _repositionTarget = transform.position + dir * _repositionDistance;
        _hasRepositionTarget = true;
    }

    private Transform GetBestPerchPoint()
    {
        if (_perchPoints == null || _perchPoints.Length == 0) return null;

        Transform best = null;
        float bestHeight = float.MinValue;
        for (int i = 0; i < _perchPoints.Length; i++)
        {
            if (_perchPoints[i] == null) continue;
            if (_perchPoints[i].position.y < transform.position.y + _perchHeightBias) continue;
            if (_perchPoints[i].position.y > bestHeight)
            {
                bestHeight = _perchPoints[i].position.y;
                best = _perchPoints[i];
            }
        }

        return best;
    }

    private void UpdateAttacks(float distance)
    {
        if (_mortarPrefab == null) return;
        if (distance > _awarenessRange) return;

        bool isCamping = _campingTimer >= _campingTimeThreshold;
        if (!_isBarrageFiring && Time.time >= _barrageReadyTime)
        {
            if (isCamping)
            {
                StartBarrage();
                return;
            }

            if (Time.time >= _nextRandomBarrageCheck)
            {
                _nextRandomBarrageCheck = Time.time + _barrageRandomCheckInterval;
                if (Random.value <= _barrageRandomChance)
                {
                    StartBarrage();
                    return;
                }
            }
        }

        if (_isBarrageFiring) return;

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _mortarInterval)
        {
            _attackTimer = 0f;
            FireMortar(GetPredictedTargetPosition());
        }
    }

    private IEnumerator BarrageRoutine()
    {
        _isBarrageFiring = true;
        _barrageReadyTime = Time.time + _barrageCooldown;

        for (int i = 0; i < _barrageCount; i++)
        {
            if (_currentTarget == null) break;
            FireMortar(GetPredictedTargetPosition());
            if (i < _barrageCount - 1)
            {
                yield return new WaitForSeconds(_barrageShotSpacing);
            }
        }

        _isBarrageFiring = false;
        _barrageRoutine = null;
    }

    private void FireMortar(Vector3 targetPos)
    {
        if (_mortarPrefab == null) return;

        Vector3 spawnPos = _mortarMuzzle != null
            ? _mortarMuzzle.position
            : transform.position + Vector3.up * 1.6f;

        var mortar = Instantiate(_mortarPrefab, spawnPos, Quaternion.identity);
        mortar.Init(spawnPos, targetPos, _mortarFlightTime, _mortarArcHeight, _mortarDamage, _mortarExplosionRadius);
    }

    private Vector3 GetPredictedTargetPosition()
    {
        Vector3 targetPos = _currentTarget != null ? _currentTarget.position : transform.position;
        targetPos += _estimatedTargetVelocity * _leadTime;
        targetPos.y = _currentTarget != null ? _currentTarget.position.y : targetPos.y;
        return targetPos;
    }

    private void UpdateTargetVelocity()
    {
        if (_currentTarget == null) return;

        float now = Time.time;
        Vector3 currentPos = _currentTarget.position;

        if (_hasTargetSample)
        {
            float dt = Mathf.Max(0.001f, now - _lastTargetSampleTime);
            _estimatedTargetVelocity = (currentPos - _lastTargetPosition) / dt;
        }

        _lastTargetPosition = currentPos;
        _lastTargetSampleTime = now;
        _hasTargetSample = true;

        if (_estimatedTargetVelocity.magnitude <= _campingSpeedThreshold)
        {
            _campingTimer += Time.deltaTime;
        }
        else
        {
            _campingTimer = 0f;
        }
    }

    private void AcquireTarget()
    {
        if (_player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerGo != null) _player = playerGo.transform;
        }

        if (_player != null && !IsTargetDead(_player))
        {
            _currentTarget = _player;
        }
    }

    private bool IsTargetDead(Transform target)
    {
        if (target == null) return true;
        var health = target.GetComponentInParent<PlayerHealth>();
        return health != null && health.IsDead;
    }

    public void ForceAggro()
    {
        _isAggroed = true;
        if (_currentTarget == null)
        {
            AcquireTarget();
        }
    }

    public bool IsStunned => Time.time < _stunnedUntil;

    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;

        float until = Time.time + seconds;
        if (until > _stunnedUntil)
        {
            _stunnedUntil = until;
        }

        StopActiveAttacks();
    }

    private void StartBarrage()
    {
        if (_barrageRoutine != null || _isBarrageFiring) return;
        _barrageRoutine = StartCoroutine(BarrageRoutine());
    }

    private void StopActiveAttacks()
    {
        if (_barrageRoutine != null)
        {
            StopCoroutine(_barrageRoutine);
            _barrageRoutine = null;
        }

        _isBarrageFiring = false;
    }
}
