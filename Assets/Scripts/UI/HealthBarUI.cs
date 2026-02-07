using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fillImage; // Image type must be Filled → Horizontal

    [Header("Options")] 
    [SerializeField] private bool updateEveryFrameIfNoEvents = false; // safety if events not firing

    private void Awake()
    {
        if (fillImage == null)
        {
            // Prefer a child Image named with "Fill"; otherwise first child Image that's not on this object
            var images = GetComponentsInChildren<Image>(true);
            Image selfImage = GetComponent<Image>();
            foreach (var img in images)
            {
                if (img == selfImage) continue;
                if (img.name.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fillImage = img;
                    break;
                }
            }
            if (fillImage == null)
            {
                foreach (var img in images)
                {
                    if (img == selfImage) continue;
                    fillImage = img;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        ResolveAndBindPlayerHealth();
        InitializeFromCurrentHealth();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveAndBindPlayerHealth();
        InitializeFromCurrentHealth();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindPlayerHealth();
    }

    private void Update()
    {
        if (updateEveryFrameIfNoEvents && playerHealth != null)
        {
            UpdateFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveAndBindPlayerHealth();
        InitializeFromCurrentHealth();
    }

    private void InitializeFromCurrentHealth()
    {
        if (playerHealth == null) return;
        UpdateFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void HandleHealthChanged(int current, int max)
    {
        UpdateFill(current, max);
    }

    private void HandleDied()
    {
        UpdateFill(0, playerHealth != null ? playerHealth.MaxHealth : 1);
    }

    private void UpdateFill(int current, int max)
    {
        if (fillImage == null || max <= 0) return;
        fillImage.fillAmount = Mathf.Clamp01((float)current / max);
    }

    private void ResolveAndBindPlayerHealth()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnDied -= HandleDied;
            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerHealth.OnDied += HandleDied;
        }
    }

    private void UnbindPlayerHealth()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnDied -= HandleDied;
    }
}
