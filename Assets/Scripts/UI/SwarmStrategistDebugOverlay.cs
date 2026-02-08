using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class SwarmStrategistDebugOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _panelRoot;
    [SerializeField] private TextMeshProUGUI _debugText;

    [Header("Behavior")]
    [SerializeField] private bool _allowRuntimeToggle = true;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F8;
    [SerializeField] private bool _showByDefaultInBuild = false;
    [SerializeField] private bool _showByDefaultInEditor = true;

    private SwarmStrategist _boundStrategist;
    private bool _isVisible;
    private string _snapshot = string.Empty;
    private string _rawResponse = string.Empty;
    private string _parsedDirective = string.Empty;
    private string _executionStatus = string.Empty;
    private bool _isConfigured;

    private void Awake()
    {
        _isConfigured = ValidateConfiguration();
        if (!_isConfigured)
        {
            enabled = false;
            return;
        }

#if UNITY_EDITOR
        _isVisible = _showByDefaultInEditor;
#else
        _isVisible = _showByDefaultInBuild;
#endif

        ApplyVisibility();
    }

    private void Update()
    {
        if (!_isConfigured)
        {
            return;
        }

        if (_allowRuntimeToggle && Input.GetKeyDown(_toggleKey))
        {
            _isVisible = !_isVisible;
            ApplyVisibility();
        }

        if (_boundStrategist != null)
        {
            SetState(
                _boundStrategist.LastSnapshotJson,
                _boundStrategist.LastRawLlmResponse,
                _boundStrategist.LastParsedDirectiveJson,
                _boundStrategist.LastExecutionStatus);
        }
    }

    /// <summary>
    /// Binds this overlay to a strategist instance.
    /// </summary>
    public void Bind(SwarmStrategist strategist)
    {
        _boundStrategist = strategist;
    }

    /// <summary>
    /// Unbinds this overlay from the strategist instance.
    /// </summary>
    public void Unbind(SwarmStrategist strategist)
    {
        if (_boundStrategist == strategist)
        {
            _boundStrategist = null;
        }
    }

    /// <summary>
    /// Updates the visible debug payload.
    /// </summary>
    public void SetState(string snapshotJson, string rawResponse, string parsedDirectiveJson, string executionStatus)
    {
        _snapshot = string.IsNullOrWhiteSpace(snapshotJson) ? "<none>" : snapshotJson;
        _rawResponse = string.IsNullOrWhiteSpace(rawResponse) ? "<none>" : rawResponse;
        _parsedDirective = string.IsNullOrWhiteSpace(parsedDirectiveJson) ? "<none>" : parsedDirectiveJson;
        _executionStatus = string.IsNullOrWhiteSpace(executionStatus) ? "<none>" : executionStatus;

        if (_debugText == null)
        {
            return;
        }

        _debugText.text =
            "SWARM STRATEGIST DEBUG\n" +
            "--------------------------\n" +
            "Execution:\n" + _executionStatus + "\n\n" +
            "Parsed Directive:\n" + _parsedDirective + "\n\n" +
            "Raw LLM Response:\n" + _rawResponse + "\n\n" +
            "Snapshot:\n" + _snapshot;
    }

    private void ApplyVisibility()
    {
        if (_panelRoot != null)
        {
            _panelRoot.gameObject.SetActive(_isVisible);
        }
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (_canvas == null)
        {
            Debug.LogError("[SwarmStrategistDebugOverlay] Missing Canvas reference.", this);
            valid = false;
        }

        if (_panelRoot == null)
        {
            Debug.LogError("[SwarmStrategistDebugOverlay] Missing Panel Root reference.", this);
            valid = false;
        }

        if (_debugText == null)
        {
            Debug.LogError("[SwarmStrategistDebugOverlay] Missing Debug Text reference.", this);
            valid = false;
        }

        return valid;
    }
}
