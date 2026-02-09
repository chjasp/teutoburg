using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ZoneHUDController : MonoBehaviour
{
    private class ZoneWidget
    {
        public Image ColorSwatch;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Status;
    }

    [Header("References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _zoneStatusRoot;
    [SerializeField] private RectTransform _captureRoot;
    [SerializeField] private Image _captureFill;
    [SerializeField] private TextMeshProUGUI _captureText;
    [SerializeField] private RectTransform _toastRoot;
    [SerializeField] private TextMeshProUGUI _toastText;
    [SerializeField] private TextMeshProUGUI _holdTimerText;

    [Header("Colors")]
    [SerializeField] private Color _enemyColor = new Color(0.92f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color _playerColor = new Color(0.2f, 0.55f, 1f, 1f);
    [SerializeField] private Color _contestedColor = new Color(0.56f, 0.56f, 0.56f, 1f);

    private readonly Dictionary<ZoneId, ZoneWidget> _widgets = new Dictionary<ZoneId, ZoneWidget>();
    private Coroutine _toastRoutine;

    /// <summary>
    /// Initializes runtime HUD elements for zone control.
    /// </summary>
    public void Initialize(CapturableZone[] zones)
    {
        EnsureCanvas();
        EnsureLayout();

        _widgets.Clear();
        for (int i = 0; i < zones.Length; i++)
        {
            CapturableZone zone = zones[i];
            if (zone == null)
            {
                continue;
            }

            ZoneWidget widget = CreateWidget(zone.Id);
            _widgets[zone.Id] = widget;
            UpdateZoneState(zone);
        }

        SetCaptureContext(null, false, 0f, null);
        SetHoldTimer(false, 0f);
    }

    /// <summary>
    /// Refreshes a zone status indicator.
    /// </summary>
    public void UpdateZoneState(CapturableZone zone)
    {
        if (zone == null || !_widgets.TryGetValue(zone.Id, out ZoneWidget widget))
        {
            return;
        }

        Color color;
        string status;

        if (zone.IsContested || zone.ActiveCaptureActor.HasValue)
        {
            color = _contestedColor;
            status = zone.IsUnderAttack ? "UNDER ATTACK" : "CONTESTED";
        }
        else
        {
            if (zone.Ownership == ZoneOwnership.Player)
            {
                color = _playerColor;
                status = "PLAYER";
            }
            else
            {
                color = _enemyColor;
                status = "ENEMY";
            }
        }

        widget.ColorSwatch.color = color;
        widget.Status.text = status;
    }

    /// <summary>
    /// Updates the contextual capture progress UI.
    /// </summary>
    public void SetCaptureContext(CapturableZone zone, bool visible, float progress01, ZoneCaptureActor? actor)
    {
        if (_captureRoot == null)
        {
            return;
        }

        _captureRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        if (_captureFill != null)
        {
            _captureFill.fillAmount = Mathf.Clamp01(progress01);
            _captureFill.color = actor == ZoneCaptureActor.Enemy ? _enemyColor : _playerColor;
        }

        if (_captureText != null)
        {
            string actorText = actor == ZoneCaptureActor.Enemy ? "Enemy Recapturing" : "Capturing";
            string zoneText = zone != null ? zone.Id.ToString() : "Zone";
            _captureText.text = $"{actorText}: {zoneText}";
        }
    }

    /// <summary>
    /// Displays a non-intrusive alert toast.
    /// </summary>
    public void ShowToast(string message)
    {
        if (_toastText == null || _toastRoot == null)
        {
            return;
        }

        if (_toastRoutine != null)
        {
            StopCoroutine(_toastRoutine);
        }

        _toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    /// <summary>
    /// Updates hold-timer visibility and text.
    /// </summary>
    public void SetHoldTimer(bool active, float remainingSeconds)
    {
        if (_holdTimerText == null)
        {
            return;
        }

        _holdTimerText.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        _holdTimerText.text = $"Hold All Zones: {Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds))}s";
    }

    private IEnumerator ToastRoutine(string message)
    {
        _toastRoot.gameObject.SetActive(true);
        _toastText.text = message;

        Color c = _toastText.color;
        c.a = 1f;
        _toastText.color = c;

        const float hold = 1.8f;
        const float fade = 0.55f;

        yield return new WaitForSeconds(hold);

        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fade);
            c.a = 1f - t;
            _toastText.color = c;
            yield return null;
        }

        _toastRoot.gameObject.SetActive(false);
        _toastRoutine = null;
    }

    private ZoneWidget CreateWidget(ZoneId zoneId)
    {
        GameObject row = new GameObject($"Zone_{zoneId}");
        row.transform.SetParent(_zoneStatusRoot, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(220f, 28f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 6f;
        layout.childControlHeight = false;
        layout.childControlWidth = false;

        GameObject swatchGo = new GameObject("Swatch");
        swatchGo.transform.SetParent(row.transform, false);
        Image swatch = swatchGo.AddComponent<Image>();
        RectTransform swatchRect = swatch.GetComponent<RectTransform>();
        swatchRect.sizeDelta = new Vector2(22f, 22f);

        TextMeshProUGUI label = CreateText(row.transform, zoneId.ToString(), 20f, TextAlignmentOptions.Left, Color.white);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(85f, 24f);

        TextMeshProUGUI status = CreateText(row.transform, "ENEMY", 20f, TextAlignmentOptions.Left, _enemyColor);
        RectTransform statusRect = status.GetComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(110f, 24f);

        return new ZoneWidget
        {
            ColorSwatch = swatch,
            Label = label,
            Status = status
        };
    }

    private void EnsureCanvas()
    {
        if (_canvas != null && _canvas.isActiveAndEnabled && IsScreenSpaceCanvas(_canvas))
        {
            return;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        _canvas = FindPreferredCanvas(canvases);

        if (_canvas == null)
        {
            GameObject canvasGo = new GameObject("ZoneHUDCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureLayout()
    {
        if (_zoneStatusRoot == null)
        {
            _zoneStatusRoot = CreatePanel("ZoneStatusPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(240f, 110f));
            VerticalLayoutGroup group = _zoneStatusRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(8, 8, 8, 8);
            group.spacing = 6f;
            group.childControlHeight = false;
            group.childControlWidth = false;
            group.childForceExpandHeight = false;
            group.childForceExpandWidth = false;
        }

        if (_captureRoot == null)
        {
            _captureRoot = CreatePanel("ZoneCapturePanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(360f, 56f));

            GameObject fillGo = new GameObject("CaptureFill");
            fillGo.transform.SetParent(_captureRoot, false);
            _captureFill = fillGo.AddComponent<Image>();
            _captureFill.type = Image.Type.Filled;
            _captureFill.fillMethod = Image.FillMethod.Horizontal;
            _captureFill.color = _playerColor;

            RectTransform fillRect = _captureFill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.02f, 0.12f);
            fillRect.anchorMax = new Vector2(0.98f, 0.58f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            _captureText = CreateText(_captureRoot, "Capturing", 24f, TextAlignmentOptions.Center, Color.white);
            RectTransform textRect = _captureText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.6f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _captureRoot.gameObject.SetActive(false);
        }

        if (_toastRoot == null)
        {
            _toastRoot = new GameObject("ZoneToast").AddComponent<RectTransform>();
            _toastRoot.SetParent(_canvas.transform, false);
            _toastRoot.anchorMin = new Vector2(0.5f, 1f);
            _toastRoot.anchorMax = new Vector2(0.5f, 1f);
            _toastRoot.pivot = new Vector2(0.5f, 1f);
            _toastRoot.anchoredPosition = new Vector2(0f, -70f);
            _toastRoot.sizeDelta = new Vector2(560f, 44f);

            _toastText = CreateText(_toastRoot, "", 26f, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.35f, 1f));
            _toastRoot.gameObject.SetActive(false);
        }

        if (_holdTimerText == null)
        {
            _holdTimerText = CreateText(_canvas.transform, "", 28f, TextAlignmentOptions.Center, Color.white);
            RectTransform rt = _holdTimerText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -46f);
            rt.sizeDelta = new Vector2(400f, 40f);
            _holdTimerText.gameObject.SetActive(false);
        }
    }

    private RectTransform CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        RectTransform panel = new GameObject(name).AddComponent<RectTransform>();
        panel.SetParent(_canvas.transform, false);
        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.pivot = anchorMax;
        panel.anchoredPosition = anchoredPos;
        panel.sizeDelta = size;

        Image bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.46f);

        return panel;
    }

    private static Canvas FindPreferredCanvas(Canvas[] canvases)
    {
        if (canvases == null)
        {
            return null;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.activeInHierarchy && IsScreenSpaceCanvas(canvas))
            {
                return canvas;
            }
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.activeInHierarchy)
            {
                return canvas;
            }
        }

        return null;
    }

    private static bool IsScreenSpaceCanvas(Canvas canvas)
    {
        return canvas != null &&
               (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.font = TMP_Settings.defaultFontAsset;

        RectTransform rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 24f);

        return tmp;
    }
}
