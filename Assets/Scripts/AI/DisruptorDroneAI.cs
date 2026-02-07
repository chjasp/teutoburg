using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class DisruptorDroneAI : MonoBehaviour, IEnemyAttackTuning, IEnemyAggro
{
    private static readonly List<DisruptorDroneAI> ActiveDrones = new List<DisruptorDroneAI>();

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private float _turnSpeed = 10f;
    [SerializeField] private float _preferredDistance = 9f;
    [SerializeField] private float _retreatDistance = 4f;
    [SerializeField] private float _awarenessRange = 20f;
    [SerializeField] private float _cohesionRange = 10f;
    [SerializeField] private float _lineOfSightStrafeStrength = 0.35f;

    [Header("Base Attack - Designator Beam")]
    [SerializeField] private float _beamRange = 12f;
    [SerializeField] private int _beamDamagePerSecond = 6;
    [SerializeField] private float _beamTickInterval = 0.25f;
    [SerializeField] private float _damageAmpMultiplier = 1.25f;
    [SerializeField] private float _damageAmpGraceTime = 0.25f;
    [SerializeField] private Transform _beamOrigin;
    [SerializeField] private LayerMask _lineOfSightMask = ~0;

    [Header("Special Attack - EMP Pulse")]
    [SerializeField] private float _empCooldown = 12f;
    [SerializeField] private float _empRadius = 5f;
    [SerializeField] private float _empSlowMultiplier = 0.6f;
    [SerializeField] private float _empSlowDuration = 3f;
    [SerializeField] private int _empRequiredReadyDrones = 2;
    [SerializeField] private float _empAllyCheckRadius = 8f;

    [Header("Targeting")]
    [SerializeField] private Transform _player;
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _rescanInterval = 0.5f;
    [SerializeField] private float _focusRetreatDuration = 1.25f;

    private CharacterController _controller;
    private float _gravityVelocityY;
    private float _scanTimer;
    private Transform _currentTarget;
    private bool _isAggroed;

    private float _beamTickTimer;
    private float _empReadyTime;

    private EnemyHealth _health;
    private int _lastHealth;
    private float _recentlyHitUntil;

    private int _baseBeamDamagePerSecond;

    void Awake()
    {
        DroneVisualRig visualRig = DroneVisualBuilder.EnsureVisuals(transform, DroneArchetype.Disruptor);
        if (_beamOrigin == null && visualRig.BeamOrigin != null)
        {
            _beamOrigin = visualRig.BeamOrigin;
        }

        _controller = GetComponent<CharacterController>();
        _baseBeamDamagePerSecond = _beamDamagePerSecond;

        if (_player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerGo != null) _player = playerGo.transform;
        }

        _health = GetComponent<EnemyHealth>();
        if (_health != null)
        {
            _lastHealth = _health.CurrentHealth;
            _health.OnHealthChanged += OnHealthChanged;
        }
    }

    void OnEnable()
    {
        if (!ActiveDrones.Contains(this))
        {
            ActiveDrones.Add(this);
        }
    }

    void OnDisable()
    {
        ActiveDrones.Remove(this);
    }

    void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnHealthChanged -= OnHealthChanged;
        }
    }

    public int BaseAttackDamage => _baseBeamDamagePerSecond;

    public void SetAttackDamage(int value)
    {
        _beamDamagePerSecond = Mathf.Max(0, value);
    }

    void Update()
    {
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

        bool hasLineOfSight = HasLineOfSight(_currentTarget);
        UpdateMovement(toTarget, distance, hasLineOfSight);
        UpdateBeamAttack(distance, hasLineOfSight);
        TryEmpPulse();
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

    private void UpdateMovement(Vector3 toTarget, float distance, bool hasLineOfSight)
    {
        Vector3 move = Vector3.zero;
        bool shouldRetreat = distance <= _retreatDistance || Time.time < _recentlyHitUntil;

        if (shouldRetreat)
        {
            move = -toTarget.normalized;
        }
        else if (distance > _preferredDistance)
        {
            move = toTarget.normalized;
        }

        if (!hasLineOfSight && move.sqrMagnitude <= 0.01f)
        {
            Vector3 strafe = Vector3.Cross(Vector3.up, toTarget.normalized) * _lineOfSightStrafeStrength;
            move = (move + strafe).normalized;
        }

        Transform ally = GetNearestAlly();
        if (ally != null && Vector3.Distance(transform.position, ally.position) > _cohesionRange && !shouldRetreat)
        {
            Vector3 toAlly = (ally.position - transform.position);
            toAlly.y = 0f;
            if (toAlly.sqrMagnitude > 0.01f)
            {
                move = (move + toAlly.normalized).normalized;
            }
        }

        _gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = new Vector3(move.x * _moveSpeed, _gravityVelocityY, move.z * _moveSpeed) * Time.deltaTime;
        var flags = _controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && _gravityVelocityY < 0f)
        {
            _gravityVelocityY = -0.5f;
        }
    }

    private void UpdateBeamAttack(float distance, bool hasLineOfSight)
    {
        if (!hasLineOfSight) return;
        if (distance > _beamRange) return;

        _beamTickTimer += Time.deltaTime;
        if (_beamTickTimer < _beamTickInterval) return;
        _beamTickTimer = 0f;

        var damageable = _currentTarget != null ? _currentTarget.GetComponentInParent<IDamageable>() : null;
        if (damageable != null)
        {
            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(_beamDamagePerSecond * _beamTickInterval));
            damageable.TakeDamage(tickDamage);
        }

        var status = _currentTarget != null ? _currentTarget.GetComponentInParent<PlayerStatusEffects>() : null;
        if (status != null)
        {
            status.ApplyDamageTakenMultiplier(_damageAmpMultiplier, _damageAmpGraceTime);
        }
    }

    private void TryEmpPulse()
    {
        if (Time.time < _empReadyTime) return;
        if (ActiveDrones.Count < _empRequiredReadyDrones) return;

        int readyCount = 0;
        for (int i = 0; i < ActiveDrones.Count; i++)
        {
            var drone = ActiveDrones[i];
            if (drone == null) continue;
            if (!drone.IsEmpReady) continue;
            if (Vector3.Distance(transform.position, drone.transform.position) > _empAllyCheckRadius) continue;
            readyCount++;
            if (readyCount >= _empRequiredReadyDrones) break;
        }

        if (readyCount >= _empRequiredReadyDrones)
        {
            FireEmpPulse();
        }
    }

    private void FireEmpPulse()
    {
        _empReadyTime = Time.time + _empCooldown;

        var hits = Physics.OverlapSphere(transform.position, _empRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].CompareTag(_playerTag)) continue;
            var status = hits[i].GetComponentInParent<PlayerStatusEffects>();
            if (status != null)
            {
                status.ApplyMoveSpeedMultiplier(_empSlowMultiplier, _empSlowDuration);
            }
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
        var playerHealth = target.GetComponentInParent<PlayerHealth>();
        return playerHealth != null && playerHealth.IsDead;
    }

    private bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Vector3 origin = _beamOrigin != null ? _beamOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 targetPos = target.position + Vector3.up * 1f;
        Vector3 dir = targetPos - origin;
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, _lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.CompareTag(_playerTag);
        }
        return false;
    }

    private Transform GetNearestAlly()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < ActiveDrones.Count; i++)
        {
            var drone = ActiveDrones[i];
            if (drone == null || drone == this) continue;
            float dist = Vector3.Distance(transform.position, drone.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = drone.transform;
            }
        }
        return best;
    }

    private void OnHealthChanged(int current, int max)
    {
        if (current < _lastHealth)
        {
            _recentlyHitUntil = Time.time + _focusRetreatDuration;
        }
        _lastHealth = current;
    }

    public bool IsEmpReady => Time.time >= _empReadyTime;

    public void ForceAggro()
    {
        _isAggroed = true;
        if (_currentTarget == null)
        {
            AcquireTarget();
        }
    }
}
