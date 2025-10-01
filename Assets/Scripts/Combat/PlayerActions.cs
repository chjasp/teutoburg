using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        // Get the Animator component attached to this GameObject
        animator = GetComponentInChildren<Animator>();
        Debug.Log("animator: " + animator);
    }

    public void CastSpell()
    {
        // Set the "CastSpell" trigger in the Animator
        Debug.Log("CastSpell");
        animator.SetTrigger("CastSpell");
    }
}