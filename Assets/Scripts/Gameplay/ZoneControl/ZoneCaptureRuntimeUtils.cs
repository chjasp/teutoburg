using UnityEngine;

/// <summary>
/// Shared capture-state calculations for capturable zones.
/// </summary>
public static class ZoneCaptureRuntimeUtils
{
    public static ZoneCaptureActor? ResolveCaptureActor(ZoneOwnership ownership, bool isPlayerInside, int aliveEnemiesInZone)
    {
        if (ownership == ZoneOwnership.Enemy)
        {
            return isPlayerInside && aliveEnemiesInZone <= 0
                ? ZoneCaptureActor.Player
                : null;
        }

        return !isPlayerInside && aliveEnemiesInZone > 0
            ? ZoneCaptureActor.Enemy
            : null;
    }

    public static float TickProgress(
        float currentProgress,
        ZoneOwnership ownership,
        ZoneCaptureActor? actor,
        int aliveEnemiesInZone,
        float playerCaptureDuration,
        float enemyRecaptureDuration,
        float progressDecayDuration,
        float deltaTime)
    {
        float progress = currentProgress;

        if (actor.HasValue)
        {
            if (actor.Value == ZoneCaptureActor.Player)
            {
                float rate = 1f / Mathf.Max(0.01f, playerCaptureDuration);
                progress = Mathf.Clamp01(progress + deltaTime * rate);
            }
            else
            {
                float rate = 1f / Mathf.Max(0.01f, enemyRecaptureDuration);
                float enemyPressure = Mathf.Clamp(aliveEnemiesInZone, 1, 4);
                float pressureMultiplier = Mathf.Lerp(1f, 1.35f, (enemyPressure - 1f) / 3f);
                rate *= pressureMultiplier;
                progress = Mathf.Clamp01(progress - deltaTime * rate);
            }
        }
        else
        {
            float target = ownership == ZoneOwnership.Player ? 1f : 0f;
            float decayRate = 1f / Mathf.Max(0.01f, progressDecayDuration);
            if (progress > 0.15f && progress < 0.85f)
            {
                decayRate *= 0.75f;
            }
            progress = Mathf.MoveTowards(progress, target, deltaTime * decayRate);
        }

        return progress;
    }

    public static bool IsUnderAttack(ZoneOwnership ownership, ZoneCaptureActor? actor)
    {
        return ownership == ZoneOwnership.Player && actor == ZoneCaptureActor.Enemy;
    }
}
