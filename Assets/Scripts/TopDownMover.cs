/* TopDownMover.cs */
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TopDownMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationLerp = 12f;
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector2 move;       // from the on-screen stick
    private Vector3 velocity;   // gravity accumulator

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null) cameraTransform = Camera.main.transform;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Debug.Log("OnMove: " + ctx.ReadValue<Vector2>());
        move = ctx.ReadValue<Vector2>(); // (-1..1, -1..1)
    }

    void Update()
    {
        // Project camera forward/right onto XZ so motion is camera-relative.
        Vector3 camFwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right,  Vector3.up).normalized;

        Vector3 worldMove = camRight * move.x + camFwd * move.y; // XZ

        // Apply movement
        Vector3 horizontal = worldMove * moveSpeed;
        velocity.y += Physics.gravity.y * Time.deltaTime; // you supply gravity when using CharacterController.Move

        Vector3 delta = (horizontal + new Vector3(0, velocity.y, 0)) * Time.deltaTime;
        var flags = controller.Move(delta);

        // If grounded, stop accumulating downward velocity
        if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f)
            velocity.y = -0.5f;

        // Face movement direction
        if (worldMove.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(worldMove);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationLerp * Time.deltaTime);
        }
    }
}