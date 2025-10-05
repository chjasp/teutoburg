using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private PlayerSummoner summoner;
    private PlayerMelee melee;

    void Start()
    {
        // Get the Animator component attached to this GameObject
        animator = GetComponentInChildren<Animator>();
        Debug.Log("animator: " + animator);
        summoner = GetComponent<PlayerSummoner>();
        melee = GetComponent<PlayerMelee>();
    }

    public void CastSpell()
    {
        // Set the "CastSpell" trigger in the Animator
        Debug.Log("CastSpell");
        animator.SetTrigger("CastSpell");
    }

    public void SummonLegionary()
    {
        Debug.Log("SummonLegionary");
        if (animator != null)
        {
            animator.SetTrigger("Summon");
        }
        else if (summoner != null)
        {
            // Fallback: summon immediately if no animator
            summoner.SummonLegionaries();
        }
    }

    public void Melee()
    {
        Debug.Log("Melee");
        if (animator != null)
        {
            animator.SetTrigger("Melee");
        }
        else if (melee != null)
        {
            melee.TriggerMelee();
        }
    }
}