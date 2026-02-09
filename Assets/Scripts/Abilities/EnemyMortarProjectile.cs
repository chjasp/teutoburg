using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyMortarProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float _flightTime = 1.2f;
    [SerializeField] private float _arcHeight = 2.5f;

    [Header("Explosion")]
    [SerializeField] private float _explosionRadius = 3f;
    [SerializeField] private int _damage = 12;
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private LayerMask _damageLayers = ~0;
    [SerializeField] private GameObject _impactVfxPrefab;

    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _elapsed;
    private bool _initialized;

    public void Init(Vector3 start, Vector3 target, float newFlightTime, float newArcHeight, int newDamage, float newRadius)
    {
        _startPosition = start;
        _targetPosition = target;
        _flightTime = Mathf.Max(0.05f, newFlightTime);
        _arcHeight = Mathf.Max(0f, newArcHeight);
        _damage = Mathf.Max(0, newDamage);
        _explosionRadius = Mathf.Max(0.1f, newRadius);
        _initialized = true;
        transform.position = _startPosition;
    }

    void Update()
    {
        if (!_initialized) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _flightTime);

        Vector3 flat = Vector3.Lerp(_startPosition, _targetPosition, t);
        float height = Mathf.Sin(Mathf.PI * t) * _arcHeight;
        transform.position = flat + Vector3.up * height;

        if (t >= 1f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (_impactVfxPrefab != null)
        {
            Instantiate(_impactVfxPrefab, transform.position, Quaternion.identity);
        }

        var hits = Physics.OverlapSphere(transform.position, _explosionRadius, _damageLayers, QueryTriggerInteraction.Collide);
        if (hits != null && hits.Length > 0)
        {
            var damaged = new HashSet<IDamageable>();
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].CompareTag(_targetTag)) continue;
                var damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable == null || damaged.Contains(damageable)) continue;
                damaged.Add(damageable);
                damageable.TakeDamage(_damage);
            }
        }

        Destroy(gameObject);
    }
}
