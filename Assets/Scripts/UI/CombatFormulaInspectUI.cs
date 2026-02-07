using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;

[DisallowMultipleComponent]
public class CombatFormulaInspectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button inspectButton;
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TextMeshProUGUI formulaBodyText;
    [SerializeField] private Button closeButton;

    private PlayerStats _playerStats;
    private bool _isModalOpen;
    private bool _didPauseGame;
    private float _timeScaleBeforePause = 1f;

    private void Awake()
    {
        EnsureLayout();
        HookButtons();
        SetModalVisible(false);
    }

    private void OnEnable()
    {
        EnsureLayout();
        HookButtons();
        ResolvePlayerStats();
    }

    private void OnDisable()
    {
        UnsubscribePlayerStats();
        HookButtons(removeOnly: true);
        EnsureGameResumed();
    }

    private void OnDestroy()
    {
        UnsubscribePlayerStats();
        EnsureGameResumed();
    }

    public void OpenInspectModal()
    {
        RefreshFormulaText();
        SetModalVisible(true);
        PauseGame();
    }

    public void CloseInspectModal()
    {
        SetModalVisible(false);
        EnsureGameResumed();
    }

    private void HandleStatsUpdated()
    {
        if (!_isModalOpen)
        {
            return;
        }

        RefreshFormulaText();
    }

    private void RefreshFormulaText()
    {
        if (formulaBodyText == null)
        {
            return;
        }

        ResolvePlayerStats();

        float drive = CombatTuning.GetDrive();
        float focus = CombatTuning.GetFocus();

        bool hasData = _playerStats != null && _playerStats.HasData;
        float calories = hasData ? _playerStats.LastCalories : 0f;
        float sleepSeconds = hasData ? _playerStats.LastSleepSeconds : 0f;

        PlayerMelee melee = FindFirstObjectByType<PlayerMelee>();
        Heartfire heartfire = FindFirstObjectByType<Heartfire>();
        EarthBreaker earthBreaker = FindFirstObjectByType<EarthBreaker>();
        LunarReckoning lunarReckoning = FindFirstObjectByType<LunarReckoning>();

        var sb = new StringBuilder(1200);

        sb.AppendLine("STAT FORMULAS");
        sb.AppendLine("Drive = Clamp((Calories / 2000) * 100, 0, 100)");
        sb.AppendLine("Focus = Clamp((SleepSeconds / 28800) * 100, 0, 100)");
        sb.AppendLine();
        sb.AppendLine("DAMAGE RULE");
        sb.AppendLine("Damage = Clamp(RoundToInt(BaseDamage + Stat * Factor), 0, 100000)");
        sb.AppendLine();

        if (hasData)
        {
            sb.AppendLine($"Live Calories: {calories:0.##}");
            sb.AppendLine($"Live SleepSeconds: {sleepSeconds:0.##}");
        }
        else
        {
            sb.AppendLine($"No PlayerStats data yet. Using CombatTuning defaults: Drive={CombatTuning.DefaultDrive:0.##}, Focus={CombatTuning.DefaultFocus:0.##}.");
        }

        sb.AppendLine($"Current Drive: {drive:0.##}");
        sb.AppendLine($"Current Focus: {focus:0.##}");
        sb.AppendLine();
        sb.AppendLine("ATTACK DAMAGE");

        if (melee != null)
        {
            sb.AppendLine($"Melee: Damage = {melee.BaseDamage} (no stat scaling) -> Current: {melee.BaseDamage}");
        }
        else
        {
            sb.AppendLine("Melee: Component not found.");
        }

        if (heartfire != null)
        {
            sb.AppendLine($"Heartfire: {heartfire.BaseDamage} + Drive * {heartfire.DriveToDamageFactor:0.###} -> Current: {heartfire.GetPreviewDamage()}");
        }
        else
        {
            sb.AppendLine("Heartfire: Component not found.");
        }

        if (earthBreaker != null)
        {
            sb.AppendLine($"EarthBreaker (per ring): {earthBreaker.DamagePerRing} + Focus * {earthBreaker.FocusToDamageFactor:0.###} -> Current: {earthBreaker.GetPreviewDamagePerRing()}");
        }
        else
        {
            sb.AppendLine("EarthBreaker: Component not found.");
        }

        if (lunarReckoning != null)
        {
            sb.AppendLine($"LunarReckoning: {lunarReckoning.BaseDamage} + Focus * {lunarReckoning.FocusToDamageFactor:0.###} -> Current: {lunarReckoning.GetPreviewDamage()}");
        }
        else
        {
            sb.AppendLine("LunarReckoning: Component not found.");
        }

        formulaBodyText.text = sb.ToString();
    }

    private void ResolvePlayerStats()
    {
        PlayerStats instance = PlayerStats.Instance;
        if (_playerStats == instance)
        {
            return;
        }

        UnsubscribePlayerStats();
        _playerStats = instance;
        if (_playerStats != null)
        {
            _playerStats.OnStatsUpdated += HandleStatsUpdated;
        }
    }

    private void UnsubscribePlayerStats()
    {
        if (_playerStats == null)
        {
            return;
        }

        _playerStats.OnStatsUpdated -= HandleStatsUpdated;
        _playerStats = null;
    }

    private void HookButtons(bool removeOnly = false)
    {
        if (inspectButton != null)
        {
            inspectButton.onClick.RemoveListener(OpenInspectModal);
            if (!removeOnly)
            {
                inspectButton.onClick.AddListener(OpenInspectModal);
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseInspectModal);
            if (!removeOnly)
            {
                closeButton.onClick.AddListener(CloseInspectModal);
            }
        }
    }

    private void PauseGame()
    {
        if (_didPauseGame)
        {
            return;
        }

        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        _didPauseGame = true;
    }

    private void EnsureGameResumed()
    {
        if (!_didPauseGame)
        {
            return;
        }

        Time.timeScale = _timeScaleBeforePause;
        _didPauseGame = false;
        _timeScaleBeforePause = 1f;
    }

    private void SetModalVisible(bool visible)
    {
        _isModalOpen = visible;
        if (modalRoot != null)
        {
            modalRoot.SetActive(visible);
        }
    }

    private void EnsureLayout()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning("[CombatFormulaInspectUI] No Canvas found.");
            return;
        }

        Transform parent = EnsureOverlayRoot(canvas.transform);

        if (inspectButton == null)
        {
            inspectButton = CreateInspectButton(parent);
        }

        if (modalRoot == null)
        {
            modalRoot = CreateModalRoot(parent);
        }

        if (formulaBodyText == null || closeButton == null)
        {
            BuildModalContentIfNeeded();
        }
    }

    private Transform EnsureOverlayRoot(Transform canvasTransform)
    {
        Transform overlay = canvasTransform.Find("CombatFormulaOverlay");
        if (overlay == null)
        {
            var go = new GameObject("CombatFormulaOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(canvasTransform, false);
            overlay = go.transform;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var overlayCanvas = go.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 300;
            overlayCanvas.pixelPerfect = false;
        }
        else
        {
            var overlayCanvas = overlay.GetComponent<Canvas>();
            if (overlayCanvas == null)
            {
                overlayCanvas = overlay.gameObject.AddComponent<Canvas>();
            }

            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 300;

            if (overlay.GetComponent<GraphicRaycaster>() == null)
            {
                overlay.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        return overlay;
    }

    private Button CreateInspectButton(Transform parent)
    {
        var go = new GameObject("InspectDamageButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -132f);
        rect.sizeDelta = new Vector2(160f, 48f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.13f, 0.13f, 0.15f, 0.95f);
        image.raycastTarget = true;

        TextMeshProUGUI label = CreateText("Label", go.transform, "Inspect Damage", 24f, TextAlignmentOptions.Center, Color.white);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private GameObject CreateModalRoot(Transform parent)
    {
        var go = new GameObject("CombatFormulaModal", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var dim = go.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        go.SetActive(false);
        return go;
    }

    private void BuildModalContentIfNeeded()
    {
        if (modalRoot == null)
        {
            return;
        }

        Transform existingPanel = modalRoot.transform.Find("CombatFormulaPanel");
        RectTransform panelRect;
        if (existingPanel == null)
        {
            var panel = new GameObject("CombatFormulaPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(modalRoot.transform, false);
            panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1160f, 700f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
            panelImage.raycastTarget = true;

            TextMeshProUGUI title = CreateText("Title", panel.transform, "Damage Formula Inspector", 40f, TextAlignmentOptions.Center, Color.white);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(1040f, 60f);
        }
        else
        {
            panelRect = existingPanel.GetComponent<RectTransform>();
        }

        if (formulaBodyText == null)
        {
            TextMeshProUGUI body = CreateText("FormulaBody", panelRect, "", 26f, TextAlignmentOptions.TopLeft, Color.white);
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(36f, 90f);
            bodyRect.offsetMax = new Vector2(-36f, -96f);
            body.textWrappingMode = TextWrappingModes.Normal;
            formulaBodyText = body;
        }

        if (closeButton == null)
        {
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panelRect, false);

            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 24f);
            closeRect.sizeDelta = new Vector2(180f, 52f);

            var closeImage = closeGo.GetComponent<Image>();
            closeImage.color = new Color(0.24f, 0.24f, 0.28f, 1f);
            closeImage.raycastTarget = true;

            TextMeshProUGUI closeLabel = CreateText("Label", closeGo.transform, "Close", 28f, TextAlignmentOptions.Center, Color.white);
            RectTransform closeLabelRect = closeLabel.rectTransform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;

            closeButton = closeGo.GetComponent<Button>();
        }
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }
}
