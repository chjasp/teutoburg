using Teutoburg.Combat;
using UnityEngine;

public class AllyHealth : NpcHealth
{
    [SerializeField] private AllyAI allyAI;
    [SerializeField] private CharacterController characterController;

    protected override void Awake()
    {
        if (allyAI == null)
        {
            allyAI = GetComponent<AllyAI>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        base.Awake();
    }

    protected override void OnBeforeDeath()
    {
        if (allyAI != null) allyAI.enabled = false;
        if (characterController != null) characterController.enabled = false;
    }
}
