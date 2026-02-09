using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ZoneCoverGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ZoneRouteNetwork _routeNetwork;
    [SerializeField] private Transform _coverRoot;

    [Header("Generation")]
    [SerializeField] private int _seed = 1419;
    [SerializeField] private int _coverPerZone = 5;
    [SerializeField] private int _coverPerLane = 4;
    [SerializeField] private float _zoneSpreadRadius = 7.5f;

    [Header("Lane Cleanup")]
    [SerializeField] private Vector2 _laneOffsetRange = new Vector2(1.4f, 3.1f);
    [SerializeField, Range(0f, 1f)] private float _alphaBravoLaneDensity = 0.35f;
    [SerializeField] private float _alphaBravoLaneCenterClearance = 3.8f;
    [SerializeField] private float _alphaBravoZoneClearance = 2.4f;
    [SerializeField] private int _placementAttemptsPerCover = 6;

    [Header("Style")]
    [SerializeField] private Vector2 _coverWidthRange = new Vector2(1.2f, 2.8f);
    [SerializeField] private Vector2 _coverHeightRange = new Vector2(1.2f, 3.4f);

    /// <summary>
    /// Generates deterministic cover around zones and along lane routes.
    /// </summary>
    public void GenerateCover()
    {
        if (_routeNetwork == null)
        {
            _routeNetwork = GetComponent<ZoneRouteNetwork>();
        }

        if (_routeNetwork == null)
        {
            return;
        }

        if (_coverRoot == null)
        {
            GameObject rootGo = new GameObject("GeneratedCover");
            rootGo.transform.SetParent(transform, false);
            _coverRoot = rootGo.transform;
        }

        ClearGeneratedCover();

        Random.InitState(_seed);

        GenerateZoneCover(ZoneId.Alpha);
        GenerateZoneCover(ZoneId.Bravo);
        GenerateZoneCover(ZoneId.Charlie);

        GenerateLaneCover(ZoneId.Alpha, ZoneId.Bravo);
        GenerateLaneCover(ZoneId.Bravo, ZoneId.Charlie);
        GenerateLaneCover(ZoneId.Alpha, ZoneId.Charlie);
    }

    private void GenerateZoneCover(ZoneId zoneId)
    {
        Vector3 center = _routeNetwork.GetZoneCenter(zoneId);
        bool preserveAlphaBravoCorridor = zoneId == ZoneId.Alpha || zoneId == ZoneId.Bravo;
        Vector3 alphaCenter = preserveAlphaBravoCorridor ? _routeNetwork.GetZoneCenter(ZoneId.Alpha) : Vector3.zero;
        Vector3 bravoCenter = preserveAlphaBravoCorridor ? _routeNetwork.GetZoneCenter(ZoneId.Bravo) : Vector3.zero;
        int attempts = Mathf.Max(1, _placementAttemptsPerCover);

        for (int i = 0; i < _coverPerZone; i++)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector2 planar = Random.insideUnitCircle * _zoneSpreadRadius;
                Vector3 pos = new Vector3(center.x + planar.x, center.y, center.z + planar.y);

                if (preserveAlphaBravoCorridor &&
                    IsNearSegmentXZ(pos, alphaCenter, bravoCenter, _alphaBravoZoneClearance))
                {
                    continue;
                }

                SpawnCoverPrimitive(pos);
                break;
            }
        }
    }

    private void GenerateLaneCover(ZoneId from, ZoneId to)
    {
        List<Vector3> points = _routeNetwork.BuildLanePath(from, to);
        if (points == null || points.Count < 2)
        {
            return;
        }

        bool isAlphaBravoLane = IsAlphaBravoLane(from, to);
        int coverCount = _coverPerLane;
        if (isAlphaBravoLane && _coverPerLane > 0)
        {
            coverCount = Mathf.Max(1, Mathf.RoundToInt(_coverPerLane * Mathf.Clamp01(_alphaBravoLaneDensity)));
        }

        float minOffset = Mathf.Min(_laneOffsetRange.x, _laneOffsetRange.y);
        float maxOffset = Mathf.Max(_laneOffsetRange.x, _laneOffsetRange.y);
        minOffset = Mathf.Max(0.1f, minOffset);
        maxOffset = Mathf.Max(minOffset + 0.01f, maxOffset);

        if (isAlphaBravoLane)
        {
            minOffset = Mathf.Max(minOffset, _alphaBravoLaneCenterClearance);
            maxOffset = Mathf.Max(maxOffset, minOffset + 1.2f);
        }

        for (int i = 0; i < coverCount; i++)
        {
            int segment = Random.Range(0, points.Count - 1);
            Vector3 a = points[segment];
            Vector3 b = points[segment + 1];

            float t = Random.Range(0.15f, 0.85f);
            Vector3 p = Vector3.Lerp(a, b, t);

            Vector3 tangent = (b - a);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.001f)
            {
                tangent = Vector3.forward;
            }

            tangent.Normalize();
            Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
            float side = Random.value < 0.5f ? -1f : 1f;

            Vector3 offset = normal * Random.Range(minOffset, maxOffset) * side;
            SpawnCoverPrimitive(p + offset);
        }
    }

    private static bool IsAlphaBravoLane(ZoneId from, ZoneId to)
    {
        bool forward = from == ZoneId.Alpha && to == ZoneId.Bravo;
        bool reverse = from == ZoneId.Bravo && to == ZoneId.Alpha;
        return forward || reverse;
    }

    private static bool IsNearSegmentXZ(Vector3 point, Vector3 a, Vector3 b, float radius)
    {
        float radiusSqr = radius * radius;
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 start = new Vector2(a.x, a.z);
        Vector2 end = new Vector2(b.x, b.z);
        Vector2 segment = end - start;

        float segmentSqr = segment.sqrMagnitude;
        if (segmentSqr <= 0.0001f)
        {
            return (p - start).sqrMagnitude <= radiusSqr;
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - start, segment) / segmentSqr);
        Vector2 nearest = start + segment * t;
        return (p - nearest).sqrMagnitude <= radiusSqr;
    }

    private void SpawnCoverPrimitive(Vector3 position)
    {
        PrimitiveType primitive = Random.value < 0.6f ? PrimitiveType.Cube : PrimitiveType.Capsule;
        GameObject cover = GameObject.CreatePrimitive(primitive);
        cover.name = "GeneratedCover";
        cover.transform.SetParent(_coverRoot, true);

        float width = Random.Range(_coverWidthRange.x, _coverWidthRange.y);
        float height = Random.Range(_coverHeightRange.x, _coverHeightRange.y);

        if (primitive == PrimitiveType.Capsule)
        {
            cover.transform.localScale = new Vector3(width * 0.6f, height * 0.6f, width * 0.6f);
            position.y += height * 0.5f;
        }
        else
        {
            float depth = Random.Range(_coverWidthRange.x, _coverWidthRange.y);
            cover.transform.localScale = new Vector3(width, height, depth);
            position.y += height * 0.5f;
        }

        cover.transform.position = position;
        cover.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Renderer renderer = cover.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        NavMeshObstacle obstacle = cover.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        if (primitive == PrimitiveType.Capsule)
        {
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = cover.transform.localScale.x * 0.5f;
            obstacle.height = cover.transform.localScale.y;
        }
        else
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = cover.transform.localScale;
        }

    }

    private void ClearGeneratedCover()
    {
        if (_coverRoot == null)
        {
            return;
        }

        var toDestroy = new List<GameObject>(_coverRoot.childCount);
        for (int i = 0; i < _coverRoot.childCount; i++)
        {
            Transform child = _coverRoot.GetChild(i);
            if (child != null)
            {
                toDestroy.Add(child.gameObject);
            }
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(toDestroy[i]);
            }
            else
#endif
            {
                Destroy(toDestroy[i]);
            }
        }
    }
}
