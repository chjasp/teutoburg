using System;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        if (_allowRuntimeToggle && WasTogglePressedThisFrame())
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

    private bool WasTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return false;
        }

        if (!TryConvertKeyCodeToInputSystemKey(_toggleKey, out Key key) || key == Key.None)
        {
            return false;
        }

        var keyControl = Keyboard.current[key];
        return keyControl != null && keyControl.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(_toggleKey);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryConvertKeyCodeToInputSystemKey(KeyCode keyCode, out Key key)
    {
        if (Enum.TryParse(keyCode.ToString(), true, out key))
        {
            return true;
        }

        switch (keyCode)
        {
            case KeyCode.Alpha0: key = Key.Digit0; return true;
            case KeyCode.Alpha1: key = Key.Digit1; return true;
            case KeyCode.Alpha2: key = Key.Digit2; return true;
            case KeyCode.Alpha3: key = Key.Digit3; return true;
            case KeyCode.Alpha4: key = Key.Digit4; return true;
            case KeyCode.Alpha5: key = Key.Digit5; return true;
            case KeyCode.Alpha6: key = Key.Digit6; return true;
            case KeyCode.Alpha7: key = Key.Digit7; return true;
            case KeyCode.Alpha8: key = Key.Digit8; return true;
            case KeyCode.Alpha9: key = Key.Digit9; return true;
            case KeyCode.Return:
                key = Key.Enter;
                return true;
            case KeyCode.KeypadEnter:
                key = Key.NumpadEnter;
                return true;
            case KeyCode.LeftControl: key = Key.LeftCtrl; return true;
            case KeyCode.RightControl: key = Key.RightCtrl; return true;
            case KeyCode.LeftShift: key = Key.LeftShift; return true;
            case KeyCode.RightShift: key = Key.RightShift; return true;
            case KeyCode.LeftAlt: key = Key.LeftAlt; return true;
            case KeyCode.RightAlt: key = Key.RightAlt; return true;
            case KeyCode.Equals: key = Key.Equals; return true;
            case KeyCode.Minus: key = Key.Minus; return true;
            case KeyCode.LeftBracket: key = Key.LeftBracket; return true;
            case KeyCode.RightBracket: key = Key.RightBracket; return true;
            case KeyCode.Backslash: key = Key.Backslash; return true;
            case KeyCode.Semicolon: key = Key.Semicolon; return true;
            case KeyCode.Quote: key = Key.Quote; return true;
            case KeyCode.Comma: key = Key.Comma; return true;
            case KeyCode.Period: key = Key.Period; return true;
            case KeyCode.Slash: key = Key.Slash; return true;
            case KeyCode.BackQuote: key = Key.Backquote; return true;
            default:
                key = Key.None;
                return false;
        }
    }
#endif
}
