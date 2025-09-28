using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimator : MonoBehaviour
{
    
    private CharacterController characterController;
    private Animator animator;

    public float movementSpeedMultiplier = 2.0f;
    public float maxSpeedFallback = 5.0f; // used if we cannot query a mover for max speed

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (animator == null)
            return;

        // CharacterController doesn't expose velocity directly. Approximate using delta position.
        // Since this runs in Update, use the difference since last frame.
        // Unity provides CharacterController.velocity starting 2020+, use it if available.
        Vector3 velocity = characterController != null ? characterController.velocity : Vector3.zero;

        // Map world velocity to local space to drive strafe/forward params
        Vector3 localVel = transform.InverseTransformDirection(new Vector3(velocity.x, 0f, velocity.z));
        float horizontalInput = Mathf.Clamp(localVel.x, -1f, 1f);
        float verticalInput = Mathf.Clamp(localVel.z, -1f, 1f);

        float maxSpeed = maxSpeedFallback;
        float currentSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float speed = Mathf.Clamp01((currentSpeed / Mathf.Max(0.0001f, maxSpeed)) * movementSpeedMultiplier);

        animator.SetFloat("Horizontal", horizontalInput);
        animator.SetFloat("Vertical", verticalInput);
        animator.SetFloat("Speed", speed);
    }
}