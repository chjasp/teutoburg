using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ZoneRouteNetwork : MonoBehaviour
{
    [Serializable]
    public class ZoneRouteDefinition
    {
        [SerializeField] private ZoneId _zoneId = ZoneId.Alpha;
        [SerializeField] private Transform _center;
        [SerializeField] private Transform[] _approachPoints = Array.Empty<Transform>();

        public ZoneId ZoneId => _zoneId;
        public Transform Center => _center;
        public Transform[] ApproachPoints => _approachPoints;

        public void SetZoneId(ZoneId zoneId)
        {
            _zoneId = zoneId;
        }

        public void SetCenter(Transform center)
        {
            _center = center;
        }

        public void SetApproachPoints(Transform[] approachPoints)
        {
            _approachPoints = approachPoints ?? Array.Empty<Transform>();
        }
    }

    [Serializable]
    public class ZoneLaneDefinition
    {
        [SerializeField] private ZoneId _from = ZoneId.Alpha;
        [SerializeField] private ZoneId _to = ZoneId.Bravo;
        [SerializeField] private Transform[] _laneWaypoints = Array.Empty<Transform>();

        public ZoneId From => _from;
        public ZoneId To => _to;
        public Transform[] LaneWaypoints => _laneWaypoints;

        public void Set(ZoneId from, ZoneId to, Transform[] laneWaypoints)
        {
            _from = from;
            _to = to;
            _laneWaypoints = laneWaypoints ?? Array.Empty<Transform>();
        }
    }

    [Header("Zone Definitions")]
    [SerializeField] private ZoneRouteDefinition[] _zones = Array.Empty<ZoneRouteDefinition>();

    [Header("Lane Definitions")]
    [SerializeField] private ZoneLaneDefinition[] _lanes = Array.Empty<ZoneLaneDefinition>();

    /// <summary>
    /// Returns the world position for the requested zone center.
    /// </summary>
    public Vector3 GetZoneCenter(ZoneId zoneId)
    {
        Transform center = GetZoneCenterTransform(zoneId);
        return center != null ? center.position : transform.position;
    }

    /// <summary>
    /// Returns the center transform for the requested zone.
    /// </summary>
    public Transform GetZoneCenterTransform(ZoneId zoneId)
    {
        ZoneRouteDefinition zone = FindZone(zoneId);
        return zone != null ? zone.Center : null;
    }

    /// <summary>
    /// Returns true when an authored zone center exists for the given zone id.
    /// </summary>
    public bool HasZone(ZoneId zoneId)
    {
        ZoneRouteDefinition zone = FindZone(zoneId);
        return zone != null && zone.Center != null;
    }

    /// <summary>
    /// Returns a random authored approach point for a zone, falling back to zone center.
    /// </summary>
    public Vector3 GetRandomApproachPoint(ZoneId zoneId)
    {
        ZoneRouteDefinition zone = FindZone(zoneId);
        if (zone != null && zone.ApproachPoints != null && zone.ApproachPoints.Length > 0)
        {
            int idx = UnityEngine.Random.Range(0, zone.ApproachPoints.Length);
            Transform approach = zone.ApproachPoints[idx];
            if (approach != null)
            {
                return approach.position;
            }
        }

        return GetZoneCenter(zoneId);
    }

    /// <summary>
    /// Builds an ordered waypoint list for travel between two zones.
    /// </summary>
    public List<Vector3> BuildLanePath(ZoneId from, ZoneId to)
    {
        var path = new List<Vector3>(8);

        ZoneLaneDefinition lane = FindLane(from, to);
        if (lane != null)
        {
            bool reverse = lane.From == to && lane.To == from;
            Transform[] points = lane.LaneWaypoints;

            if (points != null && points.Length > 0)
            {
                if (reverse)
                {
                    for (int i = points.Length - 1; i >= 0; i--)
                    {
                        if (points[i] != null)
                        {
                            path.Add(points[i].position);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (points[i] != null)
                        {
                            path.Add(points[i].position);
                        }
                    }
                }
            }
        }

        if (path.Count == 0)
        {
            path.Add(GetZoneCenter(from));
            path.Add(GetZoneCenter(to));
        }

        return path;
    }

    /// <summary>
    /// Populates default authored anchors and lanes for runtime bootstrap setup.
    /// </summary>
    public void BuildDefaultLayout(Vector3 alphaCenter, Vector3 bravoCenter, Vector3 charlieCenter)
    {
        Transform alpha = EnsureZoneAnchor("ZoneAlphaAnchor", alphaCenter);
        Transform bravo = EnsureZoneAnchor("ZoneBravoAnchor", bravoCenter);
        Transform charlie = EnsureZoneAnchor("ZoneCharlieAnchor", charlieCenter);

        Transform[] alphaApproaches = new[]
        {
            EnsurePoint("AlphaApproach_North", alphaCenter + new Vector3(6f, 0f, 5f)),
            EnsurePoint("AlphaApproach_West", alphaCenter + new Vector3(-6f, 0f, 3f))
        };

        Transform[] bravoApproaches = new[]
        {
            EnsurePoint("BravoApproach_West", bravoCenter + new Vector3(-6f, 0f, 0f)),
            EnsurePoint("BravoApproach_East", bravoCenter + new Vector3(6f, 0f, 0f))
        };

        Transform[] charlieApproaches = new[]
        {
            EnsurePoint("CharlieApproach_South", charlieCenter + new Vector3(3f, 0f, -6f)),
            EnsurePoint("CharlieApproach_West", charlieCenter + new Vector3(-5f, 0f, 2f))
        };

        _zones = new[]
        {
            new ZoneRouteDefinition(),
            new ZoneRouteDefinition(),
            new ZoneRouteDefinition()
        };

        SetZoneDefinition(_zones[0], ZoneId.Alpha, alpha, alphaApproaches);
        SetZoneDefinition(_zones[1], ZoneId.Bravo, bravo, bravoApproaches);
        SetZoneDefinition(_zones[2], ZoneId.Charlie, charlie, charlieApproaches);

        _lanes = new[]
        {
            new ZoneLaneDefinition(),
            new ZoneLaneDefinition(),
            new ZoneLaneDefinition()
        };

        _lanes[0].Set(
            ZoneId.Alpha,
            ZoneId.Bravo,
            new[]
            {
                EnsurePoint("Lane_Alpha_Bravo_1", Vector3.Lerp(alphaCenter, bravoCenter, 0.33f) + new Vector3(-2f, 0f, 1.5f)),
                EnsurePoint("Lane_Alpha_Bravo_2", Vector3.Lerp(alphaCenter, bravoCenter, 0.66f) + new Vector3(2f, 0f, -1.2f))
            });

        _lanes[1].Set(
            ZoneId.Bravo,
            ZoneId.Charlie,
            new[]
            {
                EnsurePoint("Lane_Bravo_Charlie_1", Vector3.Lerp(bravoCenter, charlieCenter, 0.33f) + new Vector3(2f, 0f, 0f)),
                EnsurePoint("Lane_Bravo_Charlie_2", Vector3.Lerp(bravoCenter, charlieCenter, 0.66f) + new Vector3(-2f, 0f, 1f))
            });

        _lanes[2].Set(
            ZoneId.Alpha,
            ZoneId.Charlie,
            new[]
            {
                EnsurePoint("Lane_Alpha_Charlie_1", Vector3.Lerp(alphaCenter, charlieCenter, 0.33f) + new Vector3(-3f, 0f, 1f)),
                EnsurePoint("Lane_Alpha_Charlie_2", Vector3.Lerp(alphaCenter, charlieCenter, 0.66f) + new Vector3(1f, 0f, 2f))
            });
    }

    private static void SetZoneDefinition(ZoneRouteDefinition zone, ZoneId id, Transform center, Transform[] approaches)
    {
        zone.SetZoneId(id);
        zone.SetCenter(center);
        zone.SetApproachPoints(approaches);
    }

    private ZoneRouteDefinition FindZone(ZoneId zoneId)
    {
        if (_zones == null)
        {
            return null;
        }

        for (int i = 0; i < _zones.Length; i++)
        {
            ZoneRouteDefinition zone = _zones[i];
            if (zone != null && zone.ZoneId == zoneId)
            {
                return zone;
            }
        }

        return null;
    }

    private ZoneLaneDefinition FindLane(ZoneId from, ZoneId to)
    {
        if (_lanes == null)
        {
            return null;
        }

        for (int i = 0; i < _lanes.Length; i++)
        {
            ZoneLaneDefinition lane = _lanes[i];
            if (lane == null)
            {
                continue;
            }

            bool forward = lane.From == from && lane.To == to;
            bool reverse = lane.From == to && lane.To == from;
            if (forward || reverse)
            {
                return lane;
            }
        }

        return null;
    }

    private Transform EnsureZoneAnchor(string name, Vector3 worldPos)
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, true);
            t = go.transform;
        }

        t.position = worldPos;
        return t;
    }

    private Transform EnsurePoint(string name, Vector3 worldPos)
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, true);
            t = go.transform;
        }

        t.position = worldPos;
        return t;
    }
}
