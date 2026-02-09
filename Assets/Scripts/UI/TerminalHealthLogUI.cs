using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Axiom.Core;

/// <summary>
/// Displays a terminal-style log for player health data.
/// </summary>
[DisallowMultipleComponent]
public class TerminalHealthLogUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _terminalText;
    [SerializeField] private Image _backgroundImage;

    [Header("Messages")]
    [SerializeField] private string _bootMessage = "Booting ...";
    [SerializeField] private bool _showBootMessage = true;

    [Header("Style (Optional)")]
    [SerializeField] private bool _applyStyleOnAwake = true;
    [SerializeField] private Color _textColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.85f);

    private PlayerStats _playerStats;

    private void Awake()
    {
        if (_terminalText == null)
        {
            _terminalText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<Image>();
        }

        if (_applyStyleOnAwake)
        {
            if (_terminalText != null)
            {
                _terminalText.color = _textColor;
            }

            if (_backgroundImage != null)
            {
                _backgroundImage.color = _backgroundColor;
            }
        }
    }

    private void OnEnable()
    {
        if (_showBootMessage)
        {
            SetBootMessage();
        }

        _playerStats = PlayerStats.Instance;
        if (_playerStats != null)
        {
            _playerStats.OnStatsUpdated += HandleStatsUpdated;

            if (_playerStats.HasData)
            {
                HandleStatsUpdated();
            }
        }
    }

    private void OnDisable()
    {
        if (_playerStats != null)
        {
            _playerStats.OnStatsUpdated -= HandleStatsUpdated;
        }
    }

    private void SetBootMessage()
    {
        if (_terminalText == null) return;
        _terminalText.text = _bootMessage;
    }

    private void HandleStatsUpdated()
    {
        if (_terminalText == null || _playerStats == null) return;

        string logLine = _playerStats.LastLogLine;
        if (string.IsNullOrEmpty(logLine)) return;

        if (_showBootMessage && !string.IsNullOrEmpty(_bootMessage))
        {
            _terminalText.text = $"{_bootMessage}\n{logLine}";
        }
        else
        {
            _terminalText.text = logLine;
        }
    }
}
