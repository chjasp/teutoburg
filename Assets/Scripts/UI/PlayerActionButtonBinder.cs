using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebinds a UI Button to the current PlayerActions after scene reloads.
/// This avoids missing OnClick references when the player is recreated.
/// </summary>
[DisallowMultipleComponent]
public class PlayerActionButtonBinder : MonoBehaviour
{
    public enum PlayerActionType
    {
        Melee,
        Heartfire,
        EarthBreaker,
        LunarReckoning,
        CastSpell
    }

    [Header("Binding")]
    [SerializeField] private PlayerActionType actionType = PlayerActionType.Melee;
    [SerializeField] private Button targetButton;
    [SerializeField] private bool bindOnEnable = true;

    private UnityAction _cachedAction;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (bindOnEnable)
        {
            BindToCurrentPlayer();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindCurrent();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindToCurrentPlayer();
    }

    public void BindToCurrentPlayer()
    {
        if (targetButton == null) return;

        UnbindCurrent();

        var playerActions = FindFirstObjectByType<PlayerActions>();
        if (playerActions == null)
        {
            Debug.LogWarning($"[PlayerActionButtonBinder] No PlayerActions found for '{gameObject.name}'.");
            return;
        }

        switch (actionType)
        {
            case PlayerActionType.Melee:
                _cachedAction = playerActions.Melee;
                break;
            case PlayerActionType.Heartfire:
                var heartfire = playerActions.GetComponent<Heartfire>();
                if (heartfire == null)
                {
                    Debug.LogWarning($"[PlayerActionButtonBinder] No Heartfire on Player for '{gameObject.name}'.");
                    return;
                }
                _cachedAction = heartfire.Cast;
                break;
            case PlayerActionType.EarthBreaker:
                var earthBreaker = playerActions.GetComponent<EarthBreaker>();
                if (earthBreaker == null)
                {
                    Debug.LogWarning($"[PlayerActionButtonBinder] No EarthBreaker on Player for '{gameObject.name}'.");
                    return;
                }
                _cachedAction = earthBreaker.CastEarthBreaker;
                break;
            case PlayerActionType.LunarReckoning:
                var lunarReckoning = playerActions.GetComponent<LunarReckoning>();
                if (lunarReckoning == null)
                {
                    Debug.LogWarning($"[PlayerActionButtonBinder] No LunarReckoning on Player for '{gameObject.name}'.");
                    return;
                }
                _cachedAction = lunarReckoning.CastAtPointer;
                break;
            case PlayerActionType.CastSpell:
                _cachedAction = playerActions.CastSpell;
                break;
        }

        if (_cachedAction != null)
        {
            targetButton.onClick.AddListener(_cachedAction);
        }
    }

    private void UnbindCurrent()
    {
        if (targetButton != null && _cachedAction != null)
        {
            targetButton.onClick.RemoveListener(_cachedAction);
        }
        _cachedAction = null;
    }
}
