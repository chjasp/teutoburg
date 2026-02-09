using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private PlayerMelee melee;

    private void Awake()
    {
        ResolveReferences();
    }

    public void CastSpell()
    {
        ResolveReferences();
        if (animator != null)
        {
            animator.SetTrigger("CastSpell");
        }
    }

    // Summon functionality removed

    public void Melee()
    {
        ResolveReferences();
        if (animator != null)
        {
            animator.SetTrigger("Melee");
        }
        else if (melee != null)
        {
            melee.TriggerMelee();
        }
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (melee == null)
        {
            melee = GetComponent<PlayerMelee>();
        }
    }
}
