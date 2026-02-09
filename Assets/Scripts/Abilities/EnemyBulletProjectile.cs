using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBulletProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _lifetime = 3f;
    [SerializeField] private float _spawnImmunityTime = 0.05f;

    [Header("Collision")]
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private LayerMask _ignoreLayers;
    [SerializeField] private bool _destroyOnEnvironment = true;

    private Vector3 _direction = Vector3.forward;
    private float _speed = 12f;
    private int _damage = 8;
    private float _lifeTimer;
    private bool _hasHit;

    public void Init(Vector3 dir, float spd, int dmg)
    {
        _direction = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        _speed = Mathf.Max(0f, spd);
        _damage = Mathf.Max(0, dmg);
        transform.forward = _direction;
    }

    void Update()
    {
        if (_hasHit) return;
        transform.position += _direction * _speed * Time.deltaTime;
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= _lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (_lifeTimer < _spawnImmunityTime) return;

        int otherLayer = other.gameObject.layer;
        if (((1 << otherLayer) & _ignoreLayers) != 0) return;

        if (other.CompareTag(_targetTag))
        {
            ApplyDamage(other);
            _hasHit = true;
            Destroy(gameObject);
            return;
        }

        if (_destroyOnEnvironment)
        {
            _hasHit = true;
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        if (_lifeTimer < _spawnImmunityTime) return;

        int otherLayer = collision.gameObject.layer;
        if (((1 << otherLayer) & _ignoreLayers) != 0) return;

        if (collision.gameObject.CompareTag(_targetTag))
        {
            ApplyDamage(collision.collider);
            _hasHit = true;
            Destroy(gameObject);
            return;
        }

        if (_destroyOnEnvironment)
        {
            _hasHit = true;
            Destroy(gameObject);
        }
    }

    private void ApplyDamage(Collider target)
    {
        var damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage);
        }
    }
}
