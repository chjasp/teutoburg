using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : HealthBase
{
    protected override void OnAfterDamageTaken(int damageAmount)
    {
        base.OnAfterDamageTaken(damageAmount);
        // Getting hit should aggro the enemy
        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.ForceAggro();
        }
    }

    protected override void OnDeathStart()
    {
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
    }
}


