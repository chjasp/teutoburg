using UnityEngine;

[DisallowMultipleComponent]
public class ZoneBeaconVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer[] _colorRenderers;
    [SerializeField] private Light _stateLight;
    [SerializeField] private ParticleSystem _ownershipFlipParticles;
    [SerializeField] private AudioSource _audioSource;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip _playerCaptureClip;
    [SerializeField] private AudioClip _enemyCaptureClip;

    [Header("Look")]
    [SerializeField] private bool _autoBuildIfMissing = true;
    [SerializeField] private float _neutralEmission = 0.55f;
    [SerializeField] private float _ownedEmission = 1.05f;

    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
        EnsureReferences();
    }

    /// <summary>
    /// Applies color/lighting based on zone ownership and contest state.
    /// </summary>
    public void ApplyState(ZoneOwnership ownership, bool isContested, bool isUnderAttack)
    {
        EnsureReferences();

        Color color;
        float intensity;

        if (isContested)
        {
            color = new Color(0.56f, 0.56f, 0.56f, 1f);
            intensity = _neutralEmission;
        }
        else
        {
            if (ownership == ZoneOwnership.Player)
            {
                color = new Color(0.2f, 0.55f, 1f, 1f);
                intensity = _ownedEmission;
            }
            else
            {
                color = new Color(0.95f, 0.2f, 0.2f, 1f);
                intensity = _ownedEmission;
            }
        }

        if (isUnderAttack)
        {
            intensity *= 1.2f;
        }

        ApplyColor(color, intensity);

        if (_stateLight != null)
        {
            _stateLight.color = color;
            _stateLight.intensity = Mathf.Lerp(1.2f, 2.3f, intensity);
        }
    }

    /// <summary>
    /// Plays ownership-flip feedback.
    /// </summary>
    public void PlayOwnershipFlip(ZoneOwnership newOwnership)
    {
        if (_ownershipFlipParticles != null)
        {
            _ownershipFlipParticles.Play();
        }

        if (_audioSource == null)
        {
            return;
        }

        AudioClip clip = newOwnership == ZoneOwnership.Player ? _playerCaptureClip : _enemyCaptureClip;
        if (clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void ApplyColor(Color color, float emission)
    {
        if (_colorRenderers == null || _colorRenderers.Length == 0)
        {
            return;
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < _colorRenderers.Length; i++)
        {
            var renderer = _colorRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            _propertyBlock.SetColor("_EmissionColor", color * emission);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void EnsureReferences()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        if (_stateLight == null)
        {
            _stateLight = GetComponentInChildren<Light>();
        }

        if ((_colorRenderers == null || _colorRenderers.Length == 0) && _autoBuildIfMissing)
        {
            BuildRuntimeBeaconVisual();
        }

        if (_colorRenderers == null || _colorRenderers.Length == 0)
        {
            _colorRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void BuildRuntimeBeaconVisual()
    {
        Transform existing = transform.Find("BeaconRuntime");
        if (existing != null)
        {
            _colorRenderers = existing.GetComponentsInChildren<Renderer>(true);
            if (_stateLight == null)
            {
                _stateLight = existing.GetComponentInChildren<Light>(true);
            }
            return;
        }

        GameObject root = new GameObject("BeaconRuntime");
        root.transform.SetParent(transform, false);

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pillar";
        pillar.transform.SetParent(root.transform, false);
        pillar.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);

        Collider pillarCollider = pillar.GetComponent<Collider>();
        if (pillarCollider != null)
        {
            Destroy(pillarCollider);
        }

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        core.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null)
        {
            Destroy(coreCollider);
        }

        GameObject lightGo = new GameObject("StateLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        _stateLight = lightGo.AddComponent<Light>();
        _stateLight.type = LightType.Point;
        _stateLight.range = 8f;
        _stateLight.intensity = 1.8f;

        _colorRenderers = root.GetComponentsInChildren<Renderer>(true);
    }
}
