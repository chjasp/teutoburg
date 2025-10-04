using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private PlayerSummoner summoner;

    void Start()
    {
        // Get the Animator component attached to this GameObject
        animator = GetComponentInChildren<Animator>();
        Debug.Log("animator: " + animator);
        summoner = GetComponent<PlayerSummoner>();
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
}