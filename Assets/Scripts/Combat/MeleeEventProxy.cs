using UnityEngine;

public class MeleeEventProxy : MonoBehaviour
{
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private AllyAI allyAI;
    [SerializeField] private PlayerMelee playerMelee;

    // Called by an Animation Event at the strike frame of the melee animation
    public void OnMeleeStrike()
    {
        if (enemyAI == null) enemyAI = GetComponentInParent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.OnMeleeStrikeEvent();
            return;
        }

        if (allyAI == null) allyAI = GetComponentInParent<AllyAI>();
        if (allyAI != null)
        {
            allyAI.OnMeleeStrikeEvent();
            return;
        }

        if (playerMelee == null) playerMelee = GetComponentInParent<PlayerMelee>();
        if (playerMelee != null)
        {
            playerMelee.OnMeleeStrikeEvent();
            return;
        }
    }
}


