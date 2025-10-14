// SpellEventProxy.cs
using UnityEngine;

public class SpellEventProxy : MonoBehaviour
{
    [SerializeField] private SpellCaster target;

    // Called by the Animation Event
    public void SpawnProjectile()
    {
        if (target == null) target = GetComponentInParent<SpellCaster>();
        if (target != null) target.SpawnProjectile();
        else Debug.LogWarning("SpellEventProxy: No SpellCaster found in parents.");
    }
}
