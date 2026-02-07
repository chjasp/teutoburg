using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class HunterDroneAI : MonoBehaviour, IEnemyAttackTuning, IEnemyAggro, IStunnable
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _turnSpeed = 10f;
    [SerializeField] private float _preferredDistance = 6f;
    [SerializeField] private float _minDistance = 3.5f;
    [SerializeField] private float _awarenessRange = 20f;

    [Header("Base Attack - Burst Fire")]
    [SerializeField] private EnemyBulletProjectile _bulletPrefab;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private float _bulletSpeed = 14f;
    [SerializeField] private int _bulletDamage = 8;
    [SerializeField] private float _fireRange = 12f;
    [SerializeField] private float _burstInterval = 2f;
    [SerializeField] private int _burstCount = 3;
    [SerializeField] private float _burstShotSpacing = 0.12f;

    [Header("Special Attack - Ram")]
    [SerializeField] private int _ramDamage = 25;
    [SerializeField] private float _ramSpeedMultiplier = 2f;
    [SerializeField] private float _ramDuration = 1f;
    [SerializeField] private float _ramTriggerRange = 6f;
    [SerializeField, Range(0f, 1f)] private float _playerLowHealthThreshold = 0.3f;
    [SerializeField] private float _ramHitRadius = 1.2f;
    [SerializeField] private float _ramCooldown = 8f;

    [Header("Targeting")]
    [SerializeField] private Transform _player;
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _rescanInterval = 0.5f;
    [SerializeField] private LayerMask _lineOfSightMask = ~0;

    private CharacterController _controller;
    private float _gravityVelocityY;
    private float _scanTimer;
    private Transform _currentTarget;
    private bool _isAggroed;

    private float _burstTimer;
    private bool _isBursting;
    private Coroutine _burstRoutine;

    private float _ramReadyTime;
    private bool _isRamming;
    private float _ramEndTime;
    private bool _ramHasHit;

    private int _baseBulletDamage;
    private int _baseRamDamage;
    private float _stunnedUntil;

    void Awake()
    {
        DroneVisualRig visualRig = DroneVisualBuilder.EnsureVisuals(transform, DroneArchetype.Hunter);
        if (_muzzle == null && visualRig.Muzzle != null)
        {
            _muzzle = visualRig.Muzzle;
        }

        _controller = GetComponent<CharacterController>();
        _baseBulletDamage = _bulletDamage;
        _baseRamDamage = _ramDamage;

        if (_player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerGo != null) _player = playerGo.transform;
        }
    }

    public int BaseAttackDamage => _baseBulletDamage;

    public void SetAttackDamage(int value)
    {
        value = Mathf.Max(0, value);
        if (_baseBulletDamage <= 0)
        {
            _bulletDamage = value;
            return;
        }

        float ratio = (float)value / _baseBulletDamage;
        _bulletDamage = value;
        _ramDamage = Mathf.RoundToInt(_baseRamDamage * ratio);
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

        if (!_isRamming && CanStartRam(distance))
        {
            StartRam();
        }

        if (_isRamming)
        {
            UpdateRamMovement();
            return;
        }

        UpdateMovement(toTarget, distance);
        UpdateBurstFire(distance);
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
        Vector3 move = Vector3.zero;
        if (distance > _preferredDistance)
        {
            move = toTarget.normalized;
        }
        else if (distance < _minDistance)
        {
            move = -toTarget.normalized;
        }

        _gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(move.x * _moveSpeed, _gravityVelocityY, move.z * _moveSpeed) * Time.deltaTime;
        var flags = _controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && _gravityVelocityY < 0f)
        {
            _gravityVelocityY = -0.5f;
        }
    }

    private void UpdateBurstFire(float distance)
    {
        if (_bulletPrefab == null) return;
        if (distance > _fireRange) return;
        if (!HasLineOfSight(_currentTarget)) return;

        _burstTimer += Time.deltaTime;
        if (_burstTimer >= _burstInterval && !_isBursting)
        {
            _burstTimer = 0f;
            _burstRoutine = StartCoroutine(BurstFireRoutine());
        }
    }

    private IEnumerator BurstFireRoutine()
    {
        _isBursting = true;
        for (int i = 0; i < _burstCount; i++)
        {
            if (_currentTarget == null) break;
            SpawnBullet(_currentTarget);
            if (i < _burstCount - 1)
            {
                yield return new WaitForSeconds(_burstShotSpacing);
            }
        }
        _isBursting = false;
        _burstRoutine = null;
    }

    private void SpawnBullet(Transform target)
    {
        if (_bulletPrefab == null || target == null) return;

        Vector3 spawnPos = _muzzle != null
            ? _muzzle.position
            : transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;

        Vector3 aimPos = target.position + Vector3.up * 1f;
        Vector3 dir = (aimPos - spawnPos).normalized;

        var bullet = Instantiate(_bulletPrefab, spawnPos, Quaternion.LookRotation(dir));
        bullet.Init(dir, _bulletSpeed, _bulletDamage);
    }

    private bool CanStartRam(float distance)
    {
        if (Time.time < _ramReadyTime) return false;
        if (distance <= _ramTriggerRange) return true;

        var playerHealth = _currentTarget != null ? _currentTarget.GetComponentInParent<PlayerHealth>() : null;
        if (playerHealth != null)
        {
            float ratio = playerHealth.MaxHealth > 0 ? (float)playerHealth.CurrentHealth / playerHealth.MaxHealth : 1f;
            if (ratio <= _playerLowHealthThreshold) return true;
        }

        return false;
    }

    private void StartRam()
    {
        _isRamming = true;
        _ramHasHit = false;
        _ramEndTime = Time.time + _ramDuration;
        _ramReadyTime = Time.time + _ramCooldown;

        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }
        _isBursting = false;
    }

    private void UpdateRamMovement()
    {
        if (_currentTarget == null)
        {
            _isRamming = false;
            return;
        }

        Vector3 toTarget = _currentTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion face = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, _turnSpeed * Time.deltaTime);
        }

        Vector3 move = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
        _gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(move.x * _moveSpeed * _ramSpeedMultiplier, _gravityVelocityY, move.z * _moveSpeed * _ramSpeedMultiplier) * Time.deltaTime;
        var flags = _controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && _gravityVelocityY < 0f)
        {
            _gravityVelocityY = -0.5f;
        }

        if (!_ramHasHit)
        {
            var hits = Physics.OverlapSphere(transform.position, _ramHitRadius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].CompareTag(_playerTag)) continue;
                var damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(_ramDamage);
                    _ramHasHit = true;
                    break;
                }
            }
        }

        if (_ramHasHit || Time.time >= _ramEndTime)
        {
            _isRamming = false;
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

    private bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Vector3 origin = _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.2f;
        Vector3 targetPos = target.position + Vector3.up * 1f;
        Vector3 dir = (targetPos - origin);
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, _lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.CompareTag(_playerTag);
        }
        return false;
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

    private void StopActiveAttacks()
    {
        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }

        _isBursting = false;
        _isRamming = false;
        _ramHasHit = false;
    }
}
