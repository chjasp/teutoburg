using UnityEngine;

public class MeleeEventProxy : MonoBehaviour
{
    [SerializeField] private PlayerMelee playerMelee;

    // Called by an Animation Event at the strike frame of the melee animation
    public void OnMeleeStrike()
    {
        if (playerMelee == null) playerMelee = GetComponentInParent<PlayerMelee>();
        if (playerMelee != null)
        {
            playerMelee.OnMeleeStrikeEvent();
            return;
        }
    }
}

