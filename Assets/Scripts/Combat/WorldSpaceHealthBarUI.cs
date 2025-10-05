using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WorldSpaceHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target; // whose head to follow
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Image fillImage; // red fill image (Filled → Horizontal)

    [Header("Health Sources (assign one)")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private EnemyHealth enemyHealth;
	[SerializeField] private AllyHealth allyHealth;

    [Header("Billboarding")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool lockYawOnly = true; // rotate about Y only, keep upright

    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;

		// Try to auto-wire a nearby health component if none assigned
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (enemyHealth == null) enemyHealth = GetComponentInParent<EnemyHealth>();
		if (allyHealth == null) allyHealth = GetComponentInParent<AllyHealth>();
        if (target == null)
        {
            // Prefer animator root or parent transform
            var animator = GetComponentInParent<Animator>();
            target = animator != null ? animator.transform : transform.parent;
        }
        if (fillImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.name.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fillImage = img;
                    break;
                }
            }
            if (fillImage == null && images.Length > 0)
            {
                fillImage = images[images.Length - 1];
            }
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerHealth.OnDied += HandleDied;
            InitializeFromCurrentHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += HandleHealthChanged;
            enemyHealth.OnDied += HandleDied;
            InitializeFromCurrentHealth(enemyHealth.CurrentHealth, enemyHealth.MaxHealth);
        }
		if (allyHealth != null)
		{
			allyHealth.OnHealthChanged += HandleHealthChanged;
			allyHealth.OnDied += HandleDied;
			InitializeFromCurrentHealth(allyHealth.CurrentHealth, allyHealth.MaxHealth);
		}
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnDied -= HandleDied;
        }
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= HandleHealthChanged;
            enemyHealth.OnDied -= HandleDied;
        }
		if (allyHealth != null)
		{
			allyHealth.OnHealthChanged -= HandleHealthChanged;
			allyHealth.OnDied -= HandleDied;
		}
    }

    private void LateUpdate()
    {
        // Follow target in world space
        if (target != null)
        {
            transform.position = target.position + worldOffset;
        }

        // Billboard to camera
        if (faceCamera && mainCam != null)
        {
            if (lockYawOnly)
            {
                Vector3 forward = transform.position - mainCam.transform.position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(forward);
                }
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
            }
        }
    }

    private void InitializeFromCurrentHealth(int current, int max)
    {
        UpdateFill(current, max);
    }

    private void HandleHealthChanged(int current, int max)
    {
        UpdateFill(current, max);
    }

	private void HandleDied()
	{
		int max = 1;
		if (playerHealth != null) max = playerHealth.MaxHealth;
		else if (enemyHealth != null) max = enemyHealth.MaxHealth;
		else if (allyHealth != null) max = allyHealth.MaxHealth;
		UpdateFill(0, max);
	}

    private void UpdateFill(int current, int max)
    {
        if (fillImage == null || max <= 0) return;
        float normalized = Mathf.Clamp01(max == 0 ? 0f : (float)current / max);
        fillImage.fillAmount = normalized;
    }
}


