using Teutoburg.Combat;
using UnityEngine;

public class EnemyHealth : NpcHealth
{
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private CharacterController characterController;

    protected override void Awake()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        base.Awake();
    }

    protected override void OnBeforeDeath()
    {
        if (enemyAI != null) enemyAI.enabled = false;
        if (characterController != null) characterController.enabled = false;
    }
}
