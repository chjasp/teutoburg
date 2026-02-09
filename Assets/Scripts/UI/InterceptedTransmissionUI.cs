using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InterceptedTransmissionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _panelRoot;
    [SerializeField] private CanvasGroup _panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private Image _blinkIndicator;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private Image _scanlineOverlay;
    [SerializeField] private AudioSource _audioSource;

    [Header("Style")]
    [SerializeField] private TMP_FontAsset _monospaceFont;
    [SerializeField] private Color _headerColor = new Color(0.35f, 1f, 0.7f, 1f);
    [SerializeField] private Color _messageColor = new Color(0.78f, 1f, 0.88f, 1f);
    [SerializeField] private Color _indicatorColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("Timing")]
    [SerializeField] private float _typewriterDurationSeconds = 1.5f;
    [SerializeField] private Vector2 _visibleSecondsRange = new Vector2(5f, 7f);
    [SerializeField] private float _fadeDurationSeconds = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip _transmissionAudioCue;

    private SwarmStrategist _boundStrategist;
    private Coroutine _playRoutine;
    private bool _isConfigured;

    private void Awake()
    {
        _isConfigured = ValidateConfiguration();
        if (!_isConfigured)
        {
            enabled = false;
            return;
        }

        ApplyStyle();
        _panelCanvasGroup.alpha = 0f;
        _panelRoot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_panelRoot == null || !_panelRoot.gameObject.activeSelf)
        {
            return;
        }

        if (_blinkIndicator != null)
        {
            bool blinkOn = Mathf.FloorToInt(Time.unscaledTime * 3.5f) % 2 == 0;
            Color color = _indicatorColor;
            color.a = blinkOn ? 1f : 0.24f;
            _blinkIndicator.color = color;
        }

        if (_scanlineOverlay != null)
        {
            Color scan = _scanlineOverlay.color;
            scan.a = 0.05f + (0.04f * Mathf.Sin(Time.unscaledTime * 12f));
            _scanlineOverlay.color = scan;
        }
    }

    /// <summary>
    /// Binds this UI to strategist transmission events.
    /// </summary>
    public void Bind(SwarmStrategist strategist)
    {
        if (_boundStrategist == strategist)
        {
            return;
        }

        Unbind(_boundStrategist);

        _boundStrategist = strategist;
        if (_boundStrategist != null)
        {
            _boundStrategist.OnTransmissionIntercepted += HandleTransmissionIntercepted;
        }
    }

    /// <summary>
    /// Unbinds this UI from strategist transmission events.
    /// </summary>
    public void Unbind(SwarmStrategist strategist)
    {
        if (strategist == null)
        {
            return;
        }

        strategist.OnTransmissionIntercepted -= HandleTransmissionIntercepted;
        if (_boundStrategist == strategist)
        {
            _boundStrategist = null;
        }
    }

    /// <summary>
    /// Displays the most recent intercepted message.
    /// </summary>
    public void ShowTransmission(string message)
    {
        if (!_isConfigured)
        {
            return;
        }

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
        }

        _playRoutine = StartCoroutine(PlayTransmissionRoutine(message));
    }

    private void HandleTransmissionIntercepted(string message)
    {
        ShowTransmission(message);
    }

    private IEnumerator PlayTransmissionRoutine(string message)
    {
        if (_panelRoot == null || _messageText == null)
        {
            yield break;
        }

        string safeMessage = string.IsNullOrWhiteSpace(message)
            ? "All positions stable. Monitoring target movement."
            : message.Trim();

        _panelRoot.gameObject.SetActive(true);
        _panelCanvasGroup.alpha = 1f;
        _messageText.text = string.Empty;

        if (_transmissionAudioCue != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_transmissionAudioCue);
        }

        float typeDuration = Mathf.Max(0.15f, _typewriterDurationSeconds);
        float elapsed = 0f;
        while (elapsed < typeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / typeDuration);
            int charCount = Mathf.Clamp(Mathf.RoundToInt(safeMessage.Length * t), 0, safeMessage.Length);
            _messageText.text = safeMessage.Substring(0, charCount);
            yield return null;
        }

        _messageText.text = safeMessage;

        float holdSeconds = Random.Range(
            Mathf.Min(_visibleSecondsRange.x, _visibleSecondsRange.y),
            Mathf.Max(_visibleSecondsRange.x, _visibleSecondsRange.y));

        yield return new WaitForSecondsRealtime(holdSeconds);

        float fadeDuration = Mathf.Max(0.05f, _fadeDurationSeconds);
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
            _panelCanvasGroup.alpha = 1f - t;
            yield return null;
        }

        _panelCanvasGroup.alpha = 0f;
        _panelRoot.gameObject.SetActive(false);
        _playRoutine = null;
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (_canvas == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Canvas reference.", this);
            valid = false;
        }

        if (_panelRoot == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Panel Root reference.", this);
            valid = false;
        }

        if (_panelCanvasGroup == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Panel CanvasGroup reference.", this);
            valid = false;
        }

        if (_headerText == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Header Text reference.", this);
            valid = false;
        }

        if (_blinkIndicator == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Blink Indicator reference.", this);
            valid = false;
        }

        if (_messageText == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Message Text reference.", this);
            valid = false;
        }

        if (_scanlineOverlay == null)
        {
            Debug.LogError("[InterceptedTransmissionUI] Missing Scanline Overlay reference.", this);
            valid = false;
        }

        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
        }

        return valid;
    }

    private void ApplyStyle()
    {
        if (_headerText != null)
        {
            _headerText.text = "SIGNAL INTERCEPTED";
            _headerText.color = _headerColor;
            if (_monospaceFont != null)
            {
                _headerText.font = _monospaceFont;
            }
        }

        if (_messageText != null)
        {
            _messageText.color = _messageColor;
            if (_monospaceFont != null)
            {
                _messageText.font = _monospaceFont;
            }
        }

        if (_blinkIndicator != null)
        {
            _blinkIndicator.color = _indicatorColor;
        }
    }
}
