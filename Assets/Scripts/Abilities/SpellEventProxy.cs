// SpellEventProxy.cs
using UnityEngine;

public class SpellEventProxy : MonoBehaviour
{
    [SerializeField] private Dynamo target;

    // Called by the Animation Event
    public void SpawnProjectile()
    {
        if (target == null) target = GetComponentInParent<Dynamo>();
        if (target != null) target.SpawnProjectile();
        else Debug.LogWarning("SpellEventProxy: No Dynamo found in parents.");
    }
}
