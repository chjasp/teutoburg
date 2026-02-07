using UnityEngine;

/// <summary>
/// Shared movement and targeting helpers used by enemy AI controllers.
/// Keeps behavior consistent while reducing copy-paste across enemy scripts.
/// </summary>
public static class EnemyAIShared
{
    /// <summary>
    /// Resolves the tagged player transform if missing and returns the result.
    /// </summary>
    public static Transform ResolvePlayer(Transform currentPlayer, string playerTag)
    {
        if (currentPlayer != null)
        {
            return currentPlayer;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return null;
        }

        GameObject playerGo = GameObject.FindGameObjectWithTag(playerTag);
        return playerGo != null ? playerGo.transform : null;
    }

    /// <summary>
    /// Applies gravity-only movement with CharacterController semantics.
    /// </summary>
    public static void ApplyGravityOnly(CharacterController controller, ref float gravityVelocityY)
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        gravityVelocityY += Physics.gravity.y * Time.deltaTime;
        CollisionFlags flags = controller.Move(new Vector3(0f, gravityVelocityY, 0f) * Time.deltaTime);
        if ((flags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
        {
            gravityVelocityY = -0.5f;
        }
    }

    /// <summary>
    /// Applies horizontal movement plus gravity using CharacterController.
    /// horizontalDirection is interpreted as a world-space normalized direction.
    /// </summary>
    public static void MoveWithGravity(CharacterController controller, Vector3 horizontalDirection, float speed, ref float gravityVelocityY)
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        gravityVelocityY += Physics.gravity.y * Time.deltaTime;

        Vector3 horizontal = horizontalDirection.sqrMagnitude > 0.001f
            ? horizontalDirection.normalized * Mathf.Max(0f, speed)
            : Vector3.zero;

        Vector3 motion = new Vector3(horizontal.x, gravityVelocityY, horizontal.z) * Time.deltaTime;
        CollisionFlags flags = controller.Move(motion);
        if ((flags & CollisionFlags.Below) != 0 && gravityVelocityY < 0f)
        {
            gravityVelocityY = -0.5f;
        }
    }

    /// <summary>
    /// Smoothly faces the actor toward the supplied horizontal direction.
    /// </summary>
    public static void FaceDirection(Transform actor, Vector3 direction, float turnSpeed)
    {
        if (actor == null)
        {
            return;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion look = Quaternion.LookRotation(direction.normalized);
        actor.rotation = Quaternion.Slerp(actor.rotation, look, Mathf.Max(0f, turnSpeed) * Time.deltaTime);
    }

    /// <summary>
    /// Extends a timer-based effect to at least now+duration.
    /// </summary>
    public static void ExtendTimer(ref float untilTime, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        float until = Time.time + durationSeconds;
        if (until > untilTime)
        {
            untilTime = until;
        }
    }

    /// <summary>
    /// Returns a negative timer offset to desynchronize simultaneous spawns.
    /// </summary>
    public static float GetDesyncedStartTimer(float intervalSeconds, float jitterFraction = 0.35f)
    {
        if (intervalSeconds <= 0f)
        {
            return 0f;
        }

        float clampedJitter = Mathf.Clamp01(jitterFraction);
        return -Random.Range(0f, intervalSeconds * clampedJitter);
    }

    /// <summary>
    /// Returns true when the target is a dead PlayerHealth or missing.
    /// </summary>
    public static bool IsDeadPlayerTarget(Transform target)
    {
        if (target == null)
        {
            return true;
        }

        PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
        return playerHealth != null && playerHealth.IsDead;
    }
}
