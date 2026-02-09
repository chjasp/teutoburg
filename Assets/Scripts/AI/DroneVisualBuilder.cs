using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum DroneArchetype
{
    Hunter = 0,
    Suppression = 1,
    Disruptor = 2
}

public readonly struct DroneVisualRig
{
    public readonly Transform Muzzle;
    public readonly Transform BeamOrigin;

    public DroneVisualRig(Transform muzzle, Transform beamOrigin)
    {
        Muzzle = muzzle;
        BeamOrigin = beamOrigin;
    }
}

public static class DroneVisualBuilder
{
    private const string DroneVisualRootName = "DroneVisualRoot";
    private const string LegacyModelRootName = "HumanMale_Character_Free";

    private static readonly Dictionary<Color32, Material> MaterialCache = new Dictionary<Color32, Material>();
    private static Shader _cachedShader;

    public static DroneVisualRig EnsureVisuals(Transform enemyRoot, DroneArchetype archetype)
    {
        if (enemyRoot == null)
        {
            return default;
        }

        HideLegacyHumanoid(enemyRoot);

        Transform existingVisuals = enemyRoot.Find(DroneVisualRootName);
        if (existingVisuals != null)
        {
            return new DroneVisualRig(
                FindChild(existingVisuals, "Muzzle"),
                FindChild(existingVisuals, "BeamOrigin"));
        }

        GameObject visualRootObject = new GameObject(DroneVisualRootName);
        visualRootObject.layer = enemyRoot.gameObject.layer;

        Transform visualRoot = visualRootObject.transform;
        visualRoot.SetParent(enemyRoot, false);
        visualRoot.localPosition = new Vector3(0f, 1.05f, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        Transform muzzle = null;
        Transform beamOrigin = null;

        switch (archetype)
        {
            case DroneArchetype.Hunter:
                muzzle = BuildHunter(visualRoot);
                break;
            case DroneArchetype.Suppression:
                muzzle = BuildSuppression(visualRoot);
                break;
            case DroneArchetype.Disruptor:
                beamOrigin = BuildDisruptor(visualRoot);
                break;
        }

        return new DroneVisualRig(muzzle, beamOrigin);
    }

    private static void HideLegacyHumanoid(Transform enemyRoot)
    {
        Transform legacyRoot = enemyRoot.Find(LegacyModelRootName);
        if (legacyRoot == null)
        {
            return;
        }

        Animator[] animators = legacyRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = false;
        }

        Renderer[] renderers = legacyRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }

    private static Transform BuildHunter(Transform root)
    {
        Color hull = new Color(0.20f, 0.22f, 0.27f);
        Color accent = new Color(0.96f, 0.33f, 0.12f);

        CreatePrimitivePart(root, PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(0.75f, 0.34f, 0.85f), hull, Vector3.zero);
        CreatePrimitivePart(root, PrimitiveType.Cube, "Nose", new Vector3(0f, 0.02f, 0.54f), new Vector3(0.24f, 0.10f, 0.56f), accent, Vector3.zero);
        CreatePrimitivePart(root, PrimitiveType.Cube, "WingLeft", new Vector3(-0.56f, -0.04f, 0f), new Vector3(0.62f, 0.05f, 0.24f), hull, new Vector3(0f, 0f, 10f));
        CreatePrimitivePart(root, PrimitiveType.Cube, "WingRight", new Vector3(0.56f, -0.04f, 0f), new Vector3(0.62f, 0.05f, 0.24f), hull, new Vector3(0f, 0f, -10f));
        CreatePrimitivePart(root, PrimitiveType.Cylinder, "TopSensor", new Vector3(0f, 0.18f, 0.24f), new Vector3(0.11f, 0.04f, 0.11f), accent, Vector3.zero);

        CreateRotor(root, "RotorFrontLeft", new Vector3(-0.42f, 0.22f, 0.28f), accent, 1250f);
        CreateRotor(root, "RotorFrontRight", new Vector3(0.42f, 0.22f, 0.28f), accent, -1250f);
        CreateRotor(root, "RotorRear", new Vector3(0f, 0.20f, -0.38f), accent, 1050f);

        return CreateAnchor(root, "Muzzle", new Vector3(0f, 0.02f, 0.92f));
    }

    private static Transform BuildSuppression(Transform root)
    {
        Color hull = new Color(0.16f, 0.20f, 0.24f);
        Color accent = new Color(0.90f, 0.61f, 0.15f);

        CreatePrimitivePart(root, PrimitiveType.Cube, "Body", Vector3.zero, new Vector3(1.05f, 0.34f, 1.00f), hull, Vector3.zero);
        CreatePrimitivePart(root, PrimitiveType.Cube, "ArmorPlate", new Vector3(0f, 0.19f, 0f), new Vector3(0.85f, 0.08f, 0.72f), hull, Vector3.zero);
        CreatePrimitivePart(root, PrimitiveType.Cylinder, "PodLeft", new Vector3(-0.36f, 0.02f, 0.30f), new Vector3(0.16f, 0.30f, 0.16f), accent, new Vector3(90f, 0f, 0f));
        CreatePrimitivePart(root, PrimitiveType.Cylinder, "PodRight", new Vector3(0.36f, 0.02f, 0.30f), new Vector3(0.16f, 0.30f, 0.16f), accent, new Vector3(90f, 0f, 0f));
        CreatePrimitivePart(root, PrimitiveType.Cube, "RearPack", new Vector3(0f, 0.06f, -0.38f), new Vector3(0.62f, 0.18f, 0.26f), accent, Vector3.zero);

        CreateRotor(root, "RotorFrontLeft", new Vector3(-0.48f, 0.30f, 0.36f), accent, 1050f);
        CreateRotor(root, "RotorFrontRight", new Vector3(0.48f, 0.30f, 0.36f), accent, -1050f);
        CreateRotor(root, "RotorRearLeft", new Vector3(-0.48f, 0.30f, -0.36f), accent, -1050f);
        CreateRotor(root, "RotorRearRight", new Vector3(0.48f, 0.30f, -0.36f), accent, 1050f);

        return CreateAnchor(root, "Muzzle", new Vector3(0f, 0.16f, 0.70f));
    }

    private static Transform BuildDisruptor(Transform root)
    {
        Color hull = new Color(0.14f, 0.18f, 0.24f);
        Color accent = new Color(0.12f, 0.84f, 0.94f);

        CreatePrimitivePart(root, PrimitiveType.Sphere, "Core", Vector3.zero, new Vector3(0.58f, 0.58f, 0.58f), hull, Vector3.zero);
        CreatePrimitivePart(root, PrimitiveType.Cylinder, "DisruptorRing", Vector3.zero, new Vector3(0.88f, 0.03f, 0.88f), accent, new Vector3(90f, 0f, 0f));
        CreatePrimitivePart(root, PrimitiveType.Cylinder, "AntennaTop", new Vector3(0f, 0.42f, 0f), new Vector3(0.06f, 0.18f, 0.06f), accent, Vector3.zero);
        CreatePrimitivePart(root, PrimitiveType.Cylinder, "AntennaFront", new Vector3(0f, 0.16f, 0.52f), new Vector3(0.05f, 0.12f, 0.05f), accent, new Vector3(90f, 0f, 0f));
        CreatePrimitivePart(root, PrimitiveType.Cube, "Emitter", new Vector3(0f, -0.18f, 0f), new Vector3(0.22f, 0.10f, 0.22f), accent, Vector3.zero);

        CreateRotor(root, "RotorLeft", new Vector3(-0.54f, 0.18f, 0f), accent, 1125f);
        CreateRotor(root, "RotorRight", new Vector3(0.54f, 0.18f, 0f), accent, -1125f);
        CreateRotor(root, "RotorRear", new Vector3(0f, 0.18f, -0.54f), accent, 1125f);

        return CreateAnchor(root, "BeamOrigin", new Vector3(0f, 0.08f, 0.66f));
    }

    private static Transform CreatePrimitivePart(
        Transform parent,
        PrimitiveType primitiveType,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        Vector3 localEulerAngles)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.layer = parent.gameObject.layer;

        Transform partTransform = part.transform;
        partTransform.SetParent(parent, false);
        partTransform.localPosition = localPosition;
        partTransform.localScale = localScale;
        partTransform.localEulerAngles = localEulerAngles;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetMaterial(color);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        return partTransform;
    }

    private static void CreateRotor(Transform parent, string rotorName, Vector3 localPosition, Color color, float spinSpeed)
    {
        GameObject rotorRootObject = new GameObject(rotorName);
        rotorRootObject.layer = parent.gameObject.layer;

        Transform rotorRoot = rotorRootObject.transform;
        rotorRoot.SetParent(parent, false);
        rotorRoot.localPosition = localPosition;
        rotorRoot.localRotation = Quaternion.identity;

        CreatePrimitivePart(rotorRoot, PrimitiveType.Cylinder, "Hub", Vector3.zero, new Vector3(0.10f, 0.02f, 0.10f), color, Vector3.zero);
        CreatePrimitivePart(rotorRoot, PrimitiveType.Cube, "BladeA", Vector3.zero, new Vector3(0.45f, 0.01f, 0.03f), color, Vector3.zero);
        CreatePrimitivePart(rotorRoot, PrimitiveType.Cube, "BladeB", Vector3.zero, new Vector3(0.03f, 0.01f, 0.45f), color, Vector3.zero);

        DroneRotorSpinner spinner = rotorRootObject.AddComponent<DroneRotorSpinner>();
        spinner.Configure(Vector3.up, spinSpeed);
    }

    private static Transform CreateAnchor(Transform parent, string anchorName, Vector3 localPosition)
    {
        GameObject anchorObject = new GameObject(anchorName);
        anchorObject.layer = parent.gameObject.layer;

        Transform anchorTransform = anchorObject.transform;
        anchorTransform.SetParent(parent, false);
        anchorTransform.localPosition = localPosition;
        anchorTransform.localRotation = Quaternion.identity;
        return anchorTransform;
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = FindChild(parent.GetChild(i), childName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static Material GetMaterial(Color color)
    {
        Color32 cacheKey = color;
        if (MaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
        {
            return cached;
        }

        Material created = new Material(GetSupportedShader());
        if (created.HasProperty("_BaseColor"))
        {
            created.SetColor("_BaseColor", color);
        }
        else if (created.HasProperty("_Color"))
        {
            created.color = color;
        }

        if (created.HasProperty("_Metallic"))
        {
            created.SetFloat("_Metallic", 0.55f);
        }

        if (created.HasProperty("_Glossiness"))
        {
            created.SetFloat("_Glossiness", 0.45f);
        }

        if (created.HasProperty("_Smoothness"))
        {
            created.SetFloat("_Smoothness", 0.45f);
        }

        if (created.HasProperty("_EmissionColor"))
        {
            Color emissionColor = color * 0.20f;
            created.SetColor("_EmissionColor", emissionColor);
            created.EnableKeyword("_EMISSION");
        }

        MaterialCache[cacheKey] = created;
        return created;
    }

    private static Shader GetSupportedShader()
    {
        if (_cachedShader != null)
        {
            return _cachedShader;
        }

        _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (_cachedShader == null)
        {
            _cachedShader = Shader.Find("Standard");
        }

        if (_cachedShader == null)
        {
            _cachedShader = Shader.Find("Sprites/Default");
        }

        return _cachedShader;
    }
}

[DisallowMultipleComponent]
public class DroneRotorSpinner : MonoBehaviour
{
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;
    [SerializeField] private float _degreesPerSecond = 1080f;

    public void Configure(Vector3 rotationAxis, float degreesPerSecond)
    {
        _rotationAxis = rotationAxis.sqrMagnitude <= 0.0001f ? Vector3.up : rotationAxis.normalized;
        _degreesPerSecond = degreesPerSecond;
    }

    private void Update()
    {
        transform.Rotate(_rotationAxis, _degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
