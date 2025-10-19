using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class LunarReckoning : MonoBehaviour
{
	[Header("Setup")]
	[SerializeField] private LunarMoon moonPrefab;
	[SerializeField] private Transform aimCamera; // used for screen-to-world ray

	[Header("Targeting")]
	[SerializeField] private LayerMask groundMask = -1; // layers considered as ground
	[SerializeField] private float maxTargetDistance = 60f;
	[SerializeField] private LineRenderer indicatorPrefab;

	private LineRenderer activeIndicator;

	[Header("Damage")]
	[SerializeField] private int damage = 250;

	// Targeting state
	private bool awaitingGroundSelection;

	void Awake()
	{
		if (aimCamera == null && Camera.main != null)
		{
			aimCamera = Camera.main.transform;
		}
	}

	// Hook this from the Input System (performed on tap/click)
	public void OnCastLunarReckoning(InputAction.CallbackContext ctx)
	{
		if (!ctx.performed) return;
		StartTargeting();
	}

	// UI Button-friendly wrapper (appears in OnClick list)
	public void CastAtPointer()
	{
		Debug.Log("CastAtPointer");
		StartTargeting();
	}

	private void StartTargeting()
	{
		awaitingGroundSelection = true;
	}

	public bool TryCastAtPointer()
	{
		if (moonPrefab == null || aimCamera == null) return false;

		Vector3 target;
		if (!TryGetGroundPointFromPointer(out target)) return false;

		SpawnMoonAt(target);
		return true;
	}

	void Update()
	{
		if (!awaitingGroundSelection) return;
		Vector3 point;
		if (TryGetGroundTap(out point))
		{
			awaitingGroundSelection = false;
			SpawnMoonAt(point);
		}
	}

	private bool TryGetGroundPointFromPointer(out Vector3 point)
	{
		point = default;
		if (Mouse.current != null)
		{
			Vector2 screen = Mouse.current.position.ReadValue();
			return RaycastFromScreen(screen, out point);
		}
		// Touch primary
		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
		{
			Vector2 screen = Touchscreen.current.primaryTouch.position.ReadValue();
			return RaycastFromScreen(screen, out point);
		}
		return false;
	}

	private bool TryGetGroundTap(out Vector3 point)
	{
		point = default;
		// Touch: wait for release to avoid consuming the UI button press
		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
		{
			int fingerId = Touchscreen.current.primaryTouch.touchId.ReadValue();
			if (IsPointerOverUI(fingerId)) return false;
			Vector2 screen = Touchscreen.current.primaryTouch.position.ReadValue();
			return RaycastFromScreen(screen, out point);
		}
		// Mouse
		if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
		{
			if (IsPointerOverUI(-1)) return false; // -1 for mouse pointer id
			Vector2 screen = Mouse.current.position.ReadValue();
			return RaycastFromScreen(screen, out point);
		}
		return false;
	}

	private bool RaycastFromScreen(Vector2 screenPos, out Vector3 point)
	{
		point = default;
		var cam = aimCamera.GetComponent<Camera>();
		if (cam == null) cam = Camera.main;
		if (cam == null) return false;

		Ray ray = cam.ScreenPointToRay(screenPos);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit, maxTargetDistance, groundMask, QueryTriggerInteraction.Ignore))
		{
			point = hit.point;
			// Preview indicator while aiming (on drag move or hover)
			ShowIndicator(point);
			return true;
		}
		return false;
	}

	private bool IsPointerOverUI(int pointerId)
	{
		if (EventSystem.current == null) return false;
		return EventSystem.current.IsPointerOverGameObject(pointerId);
	}

	private void SpawnMoonAt(Vector3 groundPoint)
	{
		if (moonPrefab == null)
		{
			Debug.LogWarning("LunarReckoning: moonPrefab is not assigned. Assign a LunarMoon prefab on the component.");
			return;
		}
		var moon = Instantiate(moonPrefab);
		moon.InitAtTarget(groundPoint, damage);
		if (activeIndicator != null)
		{
			moon.SetIndicatorToDestroy(activeIndicator.gameObject);
			activeIndicator = null;
		}
	}

	private void ShowIndicator(Vector3 center)
	{
		if (indicatorPrefab == null) return;
		if (activeIndicator == null)
		{
			activeIndicator = Instantiate(indicatorPrefab);
			activeIndicator.loop = true;
			activeIndicator.useWorldSpace = true;
		}
		float radius = moonPrefab != null ? moonPrefab.Radius : 3.5f;
		int segments = Mathf.Max(32, activeIndicator.positionCount > 0 ? activeIndicator.positionCount : 48);
		activeIndicator.positionCount = segments;
		float angleStep = Mathf.PI * 2f / segments;
		float y = center.y + 0.05f;
		for (int i = 0; i < segments; i++)
		{
			float a = angleStep * i;
			float x = Mathf.Cos(a) * radius;
			float z = Mathf.Sin(a) * radius;
			activeIndicator.SetPosition(i, new Vector3(center.x + x, y, center.z + z));
		}
	}
}


