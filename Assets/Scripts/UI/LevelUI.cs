using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Axiom.Core;

/// <summary>
/// Displays the current level at the top center of the screen.
/// </summary>
[DisallowMultipleComponent]
public class LevelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Display Format")]
    [SerializeField] private string levelFormat = "Level {0}";

    private void Start()
    {
        // Auto-find text component if not assigned
        if (levelText == null)
        {
            levelText = GetComponentInChildren<TextMeshProUGUI>();
        }

        // Subscribe to level changes
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelChanged += UpdateLevelDisplay;
            UpdateLevelDisplay(LevelManager.Instance.CurrentLevel);
        }
    }

    private void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelChanged -= UpdateLevelDisplay;
        }
    }

    private void UpdateLevelDisplay(int level)
    {
        if (levelText != null)
        {
            levelText.text = string.Format(levelFormat, level);
        }
    }

    /// <summary>
    /// Creates a LevelUI prefab in the scene programmatically.
    /// Call this from a setup script if you don't want to manually create the UI.
    /// </summary>
    public static LevelUI CreateInScene()
    {
        // Find or create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // Create LevelUI GameObject
        var levelUIGo = new GameObject("LevelUI");
        levelUIGo.transform.SetParent(canvas.transform, false);

        // Setup RectTransform for top center
        var rect = levelUIGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(200f, 50f);

        // Add TextMeshPro
        var text = levelUIGo.AddComponent<TextMeshProUGUI>();
        text.text = "Level 1";
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;

        // Add outline for visibility
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;

        // Add LevelUI component
        var levelUI = levelUIGo.AddComponent<LevelUI>();
        levelUI.levelText = text;

        return levelUI;
    }
}
