using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private PlayerMelee melee;

    void Awake()
    {
        // Get references in Awake for faster availability
        animator = GetComponentInChildren<Animator>();
        melee = GetComponent<PlayerMelee>();
    }

    void Start()
    {
        // Re-acquire in Start as safety
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (melee == null) melee = GetComponent<PlayerMelee>();
        
        Debug.Log($"[PlayerActions] Initialized. Animator: {(animator != null ? animator.name : "NULL")}, Melee: {(melee != null ? "OK" : "NULL")}");
    }

    public void CastSpell()
    {
        Debug.Log("[PlayerActions] CastSpell() called");
        if (animator != null)
        {
        animator.SetTrigger("CastSpell");
        }
        else
        {
            Debug.LogWarning("[PlayerActions] CastSpell failed - animator is null!");
        }
    }

    // Summon functionality removed

    public void Melee()
    {
        Debug.Log("[PlayerActions] Melee() called");
        if (animator != null)
        {
            animator.SetTrigger("Melee");
        }
        else if (melee != null)
        {
            Debug.Log("[PlayerActions] Using fallback melee.TriggerMelee()");
            melee.TriggerMelee();
        }
        else
        {
            Debug.LogWarning("[PlayerActions] Melee failed - both animator and melee are null!");
        }
    }
}