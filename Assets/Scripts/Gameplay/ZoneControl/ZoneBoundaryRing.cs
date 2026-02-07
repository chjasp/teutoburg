using UnityEngine;

[DisallowMultipleComponent]
public class ZoneBoundaryRing : MonoBehaviour
{
    [Header("Ring")]
    [SerializeField] private float _radius = 10f;
    [SerializeField] private int _segments = 64;
    [SerializeField] private float _width = 0.2f;
    [SerializeField] private float _yOffset = 0.05f;
    [SerializeField] private Material _lineMaterial;

    [Header("References")]
    [SerializeField] private LineRenderer _lineRenderer;

    public float Radius => _radius;

    private void Awake()
    {
        EnsureRenderer();
        Rebuild();
    }

    private void OnValidate()
    {
        _radius = Mathf.Max(0.1f, _radius);
        _segments = Mathf.Max(8, _segments);
        _width = Mathf.Max(0.01f, _width);

        if (!Application.isPlaying)
        {
            EnsureRenderer();
            Rebuild();
        }
    }

    /// <summary>
    /// Sets the ring radius and rebuilds its geometry.
    /// </summary>
    public void SetRadius(float radius)
    {
        _radius = Mathf.Max(0.1f, radius);
        Rebuild();
    }

    /// <summary>
    /// Sets the ring color.
    /// </summary>
    public void SetColor(Color color)
    {
        EnsureRenderer();
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
        _lineRenderer.colorGradient = BuildSolidGradient(color);

        Material material = Application.isPlaying ? _lineRenderer.material : _lineRenderer.sharedMaterial;
        ApplyMaterialColor(material, color);
    }

    /// <summary>
    /// Rebuilds the line-rendered circular ring.
    /// </summary>
    public void Rebuild()
    {
        EnsureRenderer();

        _lineRenderer.positionCount = _segments;
        float step = Mathf.PI * 2f / _segments;

        for (int i = 0; i < _segments; i++)
        {
            float angle = i * step;
            float x = Mathf.Cos(angle) * _radius;
            float z = Mathf.Sin(angle) * _radius;
            _lineRenderer.SetPosition(i, new Vector3(x, _yOffset, z));
        }
    }

    private void EnsureRenderer()
    {
        if (_lineRenderer == null)
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        if (_lineRenderer == null)
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.widthMultiplier = _width;
        _lineRenderer.numCornerVertices = 3;
        _lineRenderer.numCapVertices = 3;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        if (_lineRenderer.sharedMaterial == null)
        {
            _lineRenderer.sharedMaterial = _lineMaterial != null
                ? _lineMaterial
                : new Material(Shader.Find("Sprites/Default"));
        }
        else if (!SupportsColor(_lineRenderer.sharedMaterial))
        {
            if (_lineMaterial != null && SupportsColor(_lineMaterial))
            {
                _lineRenderer.sharedMaterial = _lineMaterial;
            }
            else
            {
                _lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        ApplyMaterialColor(_lineRenderer.sharedMaterial, _lineRenderer.startColor);
    }

    private static Gradient BuildSolidGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 1f)
            });
        return gradient;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 0.8f);
        }
    }

    private static bool SupportsColor(Material material)
    {
        if (material == null)
        {
            return false;
        }

        return material.HasProperty("_Color") || material.HasProperty("_BaseColor");
    }
}
