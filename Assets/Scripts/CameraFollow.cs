using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // The target the camera should follow (your player)
    public Transform target;

    // How fast the camera moves to the target position
    public float smoothSpeed = 0.125f;

    // The offset from the target (x, y, z)
    public Vector3 offset;

    // This method is called after all Update functions have been called
    void LateUpdate()
    {
        // Check if a target has been assigned
        if (target != null)
        {
            // Calculate the desired position for the camera
            Vector3 desiredPosition = target.position + offset;

            // Smoothly move from the current position to the desired position
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

            // Apply the new position to the camera
            transform.position = smoothedPosition;

            // Make the camera look at the player's position
            transform.LookAt(target);
        }
    }
}