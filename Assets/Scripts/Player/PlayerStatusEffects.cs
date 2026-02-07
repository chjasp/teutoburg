using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStatusEffects : MonoBehaviour
{
    [Header("Damage Taken")]
    [SerializeField, Min(1f)] private float _defaultDamageTakenMultiplier = 1f;

    [Header("Move Speed")]
    [SerializeField, Range(0.05f, 1f)] private float _defaultMoveSpeedMultiplier = 1f;

    private float _damageTakenMultiplier = 1f;
    private float _damageTakenUntil = -1f;

    private float _moveSpeedMultiplier = 1f;
    private float _moveSpeedUntil = -1f;

    public float DamageTakenMultiplier => _damageTakenMultiplier;
    public float MoveSpeedMultiplier => _moveSpeedMultiplier;

    void Awake()
    {
        _damageTakenMultiplier = Mathf.Max(1f, _defaultDamageTakenMultiplier);
        _moveSpeedMultiplier = Mathf.Clamp(_defaultMoveSpeedMultiplier, 0.05f, 1f);
    }

    void Update()
    {
        if (Time.time >= _damageTakenUntil)
        {
            _damageTakenMultiplier = Mathf.Max(1f, _defaultDamageTakenMultiplier);
        }

        if (Time.time >= _moveSpeedUntil)
        {
            _moveSpeedMultiplier = Mathf.Clamp(_defaultMoveSpeedMultiplier, 0.05f, 1f);
        }
    }

    /// <summary>
    /// Applies a temporary multiplier to damage taken by the player.
    /// </summary>
    public void ApplyDamageTakenMultiplier(float multiplier, float duration)
    {
        if (duration <= 0f) return;
        multiplier = Mathf.Max(1f, multiplier);

        float until = Time.time + duration;
        if (multiplier > _damageTakenMultiplier || until > _damageTakenUntil)
        {
            _damageTakenMultiplier = Mathf.Max(multiplier, _damageTakenMultiplier);
            _damageTakenUntil = Mathf.Max(until, _damageTakenUntil);
        }
    }

    /// <summary>
    /// Applies a temporary multiplier to the player's move speed.
    /// </summary>
    public void ApplyMoveSpeedMultiplier(float multiplier, float duration)
    {
        if (duration <= 0f) return;
        multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);

        float until = Time.time + duration;
        if (multiplier < _moveSpeedMultiplier || until > _moveSpeedUntil)
        {
            _moveSpeedMultiplier = Mathf.Min(multiplier, _moveSpeedMultiplier);
            _moveSpeedUntil = Mathf.Max(until, _moveSpeedUntil);
        }
    }
}
