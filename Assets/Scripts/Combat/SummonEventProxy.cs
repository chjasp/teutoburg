using UnityEngine;

public class SummonEventProxy : MonoBehaviour
{
    [SerializeField] private PlayerSummoner target;

    // Called by an Animation Event on the Summon animation
    public void SummonLegionaries()
    {
        if (target == null) target = GetComponentInParent<PlayerSummoner>();
        if (target != null) target.SummonLegionaries();
        else Debug.LogWarning("SummonEventProxy: No PlayerSummoner found in parents.");
    }
}


