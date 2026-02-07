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

        for (int i = 0; i < _coverPerZone; i++)
        {
            Vector2 planar = Random.insideUnitCircle * _zoneSpreadRadius;
            Vector3 pos = new Vector3(center.x + planar.x, center.y, center.z + planar.y);
            SpawnCoverPrimitive(pos);
        }
    }

    private void GenerateLaneCover(ZoneId from, ZoneId to)
    {
        List<Vector3> points = _routeNetwork.BuildLanePath(from, to);
        if (points == null || points.Count < 2)
        {
            return;
        }

        for (int i = 0; i < _coverPerLane; i++)
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

            Vector3 offset = normal * Random.Range(1.4f, 3.1f) * side;
            SpawnCoverPrimitive(p + offset);
        }
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
