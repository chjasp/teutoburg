using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSummoner : MonoBehaviour
{
    [Header("Summon Setup")]
    [SerializeField] private GameObject legionaryPrefab;
    [SerializeField] private Transform summonOrigin; // optional: where to spawn around; default = player
    [SerializeField] private float ringRadius = 1.5f;
    [SerializeField] private int count = 3;

    [Header("Lifetime (optional)")]
    [SerializeField] private float legionaryLifetime = 20f; // 0 = infinite

    public void SummonLegionaries()
    {
        if (legionaryPrefab == null)
        {
            Debug.LogWarning("PlayerSummoner: legionaryPrefab is not assigned.");
            return;
        }

        Transform origin = summonOrigin != null ? summonOrigin : transform;

        float angleStep = Mathf.PI * 2f / Mathf.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            float angle = angleStep * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            Vector3 spawnPos = origin.position + offset;
            Quaternion rot = Quaternion.LookRotation((origin.forward + offset.normalized).normalized);

            var go = Object.Instantiate(legionaryPrefab, spawnPos, rot);
            if (legionaryLifetime > 0f)
            {
                var lifetime = go.GetComponent<Lifetime>();
                if (lifetime == null) lifetime = go.AddComponent<Lifetime>();
                lifetime.SetLifetime(legionaryLifetime);
            }
        }
    }
}


