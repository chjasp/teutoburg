using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private PlayerMelee melee;

    void Start()
    {
        // Get the Animator component attached to this GameObject
        animator = GetComponentInChildren<Animator>();
        Debug.Log("animator: " + animator);
        melee = GetComponent<PlayerMelee>();
    }

    public void CastSpell()
    {
        // Set the "CastSpell" trigger in the Animator
        Debug.Log("CastSpell");
        animator.SetTrigger("CastSpell");
    }

    // Summon functionality removed

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