using UnityEngine;
using UnityEngine.UI;

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
        // Try to auto-find a PlayerHealth in scene if not assigned
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

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
        // Initialize once more in Start to ensure PlayerHealth has finished its Awake initialization
        InitializeFromCurrentHealth();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerHealth.OnDied += HandleDied;
            // Initialize UI with current values on enable
            InitializeFromCurrentHealth();
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnDied -= HandleDied;
        }
    }

    private void Update()
    {
        // Re-acquire player reference if lost (handles scene reload)
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                playerHealth.OnDied += HandleDied;
                InitializeFromCurrentHealth();
            }
        }

        if (updateEveryFrameIfNoEvents && playerHealth != null)
        {
            UpdateFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
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
}
