using UnityEngine;
using UnityEngine.AI;

public class PlayerAnimator : MonoBehaviour
{
    // Reference to the NavMeshAgent component
    private NavMeshAgent agent;

    // Reference to the Animator component
    private Animator animator;

    // A multiplier for movement speed to make animations more dynamic
    // This helps push the MoveSpeed parameter towards 1.0 during movement
    public float movementSpeedMultiplier = 2.0f;

    void Awake()
    {
        // Get the NavMeshAgent component attached to this GameObject
        agent = GetComponent<NavMeshAgent>();

        // Get the Animator component from the child object
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Get the agent's desired velocity (intended movement direction)
        Vector3 desiredVelocity = agent.desiredVelocity;

        // Calculate horizontal and vertical components from desired velocity
        float horizontalInput = 0f;
        float verticalInput = 0f;

        if (desiredVelocity != Vector3.zero)
        {
            // Normalize the desired velocity to get direction components
            Vector3 normalizedDesired = desiredVelocity.normalized;
            horizontalInput = normalizedDesired.x;
            print("normalizedDesired.y: " + normalizedDesired.y);
            print("normalizedDesired.x: " + normalizedDesired.x);
            verticalInput = normalizedDesired.y; // CHANGED BY ME
        }

        // Calculate the magnitude of the current velocity
        // Apply multiplier to make movement animations more pronounced
        float currentSpeed = agent.velocity.magnitude;
        float speed = Mathf.Clamp01((currentSpeed / agent.speed) * movementSpeedMultiplier);

        // Pass all values to the Animator
        animator.SetFloat("Horizontal", horizontalInput);
        animator.SetFloat("Vertical", verticalInput);
        animator.SetFloat("Speed", speed);
    }
}