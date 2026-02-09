using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared utility logic for zone-control runtime bookkeeping.
/// </summary>
public static class ZoneControlRuntimeUtils
{
    public static bool AreAllZonesPlayerOwned(CapturableZone[] zones)
    {
        if (zones == null || zones.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            CapturableZone zone = zones[i];
            if (zone == null || zone.Ownership != ZoneOwnership.Player)
            {
                return false;
            }
        }

        return true;
    }

    public static CapturableZone FindFirstPlayerZone(CapturableZone[] zones)
    {
        if (zones == null)
        {
            return null;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            CapturableZone zone = zones[i];
            if (zone != null && zone.IsPlayerInside)
            {
                return zone;
            }
        }

        return null;
    }

    public static Dictionary<ZoneId, CapturableZone> BuildZoneLookup(CapturableZone[] zones)
    {
        var lookup = new Dictionary<ZoneId, CapturableZone>(3);
        if (zones == null)
        {
            return lookup;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            CapturableZone zone = zones[i];
            if (zone != null)
            {
                lookup[zone.Id] = zone;
            }
        }

        return lookup;
    }
}

/// <summary>
/// Tracks the hold-all-zones win condition countdown.
/// </summary>
public sealed class ZoneHoldTimerState
{
    private readonly float _configuredDuration;
    private bool _isActive;
    private float _remaining;

    public ZoneHoldTimerState(float durationSeconds)
    {
        _configuredDuration = Mathf.Max(0.01f, durationSeconds);
        _remaining = _configuredDuration;
        _isActive = false;
    }

    public bool IsActive => _isActive;
    public float Remaining => _remaining;

    public void Reset()
    {
        _isActive = false;
        _remaining = _configuredDuration;
    }

    public void Tick(float deltaTime)
    {
        if (!_isActive)
        {
            _isActive = true;
            _remaining = _configuredDuration;
            return;
        }

        _remaining -= Mathf.Max(0f, deltaTime);
    }
}
