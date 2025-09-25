using UnityEngine;
using UnityEngine.AI;

public class PlayerAnimator : MonoBehaviour
{
    // Reference to the NavMeshAgent component
    private NavMeshAgent agent;

    // Reference to the Animator component
    private Animator animator;

    void Awake()
    {
        // Get the NavMeshAgent component attached to this GameObject
        agent = GetComponent<NavMeshAgent>();

        // Get the Animator component from the child object
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Calculate the agent's current speed (magnitude of its velocity)
        float speed = agent.velocity.magnitude;

        // Set the "Speed" parameter in the Animator to the agent's current speed
        // The "Speed" string must match the parameter name you created in the Animator
        animator.SetFloat("Speed", speed);
    }
}