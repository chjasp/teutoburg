using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;

[DisallowMultipleComponent]
public class ZoneEndScreenUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private Button _retryButton;

    /// <summary>
    /// Ensures end-screen UI is built and hidden.
    /// </summary>
    public void Initialize()
    {
        EnsureCanvas();
        EnsureLayout();
        Hide();
    }

    /// <summary>
    /// Shows the victory state screen.
    /// </summary>
    public void ShowVictory()
    {
        if (_panelRoot == null)
        {
            return;
        }

        _panelRoot.SetActive(true);
        if (_titleText != null)
        {
            _titleText.text = "Victory";
            _titleText.color = new Color(0.2f, 0.55f, 1f, 1f);
        }

        if (_bodyText != null)
        {
            _bodyText.text = "All zones held. Frontline secured.";
        }
    }

    /// <summary>
    /// Shows the defeat state screen.
    /// </summary>
    public void ShowDefeat()
    {
        if (_panelRoot == null)
        {
            return;
        }

        _panelRoot.SetActive(true);
        if (_titleText != null)
        {
            _titleText.text = "Defeat";
            _titleText.color = new Color(0.95f, 0.2f, 0.2f, 1f);
        }

        if (_bodyText != null)
        {
            _bodyText.text = "You were overrun.";
        }
    }

    /// <summary>
    /// Hides the end screen.
    /// </summary>
    public void Hide()
    {
        if (_panelRoot != null)
        {
            _panelRoot.SetActive(false);
        }
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
            GameObject canvasGo = new GameObject("ZoneEndCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureLayout()
    {
        if (_panelRoot != null)
        {
            return;
        }

        _panelRoot = new GameObject("ZoneEndPanel");
        _panelRoot.transform.SetParent(_canvas.transform, false);

        RectTransform panelRect = _panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image dim = _panelRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        _titleText = CreateText(_panelRoot.transform, "Victory", 72f, TextAlignmentOptions.Center, Color.white);
        RectTransform titleRect = _titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.68f);
        titleRect.anchorMax = new Vector2(0.5f, 0.68f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(700f, 90f);

        _bodyText = CreateText(_panelRoot.transform, "", 34f, TextAlignmentOptions.Center, Color.white);
        RectTransform bodyRect = _bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 0.57f);
        bodyRect.anchorMax = new Vector2(0.5f, 0.57f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.sizeDelta = new Vector2(760f, 80f);

        GameObject retryGo = new GameObject("RetryButton");
        retryGo.transform.SetParent(_panelRoot.transform, false);

        RectTransform retryRect = retryGo.AddComponent<RectTransform>();
        retryRect.anchorMin = new Vector2(0.5f, 0.42f);
        retryRect.anchorMax = new Vector2(0.5f, 0.42f);
        retryRect.pivot = new Vector2(0.5f, 0.5f);
        retryRect.sizeDelta = new Vector2(260f, 62f);

        Image retryImage = retryGo.AddComponent<Image>();
        retryImage.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        _retryButton = retryGo.AddComponent<Button>();
        _retryButton.targetGraphic = retryImage;
        _retryButton.onClick.AddListener(OnRetryClicked);

        TextMeshProUGUI retryText = CreateText(retryGo.transform, "Retry", 36f, TextAlignmentOptions.Center, Color.black);
        RectTransform retryTextRect = retryText.GetComponent<RectTransform>();
        retryTextRect.anchorMin = Vector2.zero;
        retryTextRect.anchorMax = Vector2.one;
        retryTextRect.offsetMin = Vector2.zero;
        retryTextRect.offsetMax = Vector2.zero;
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
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.font = TMP_Settings.defaultFontAsset;

        return tmp;
    }

    private void OnRetryClicked()
    {
        Hide();
        GameManager.Instance.ResetRun();
    }
}
