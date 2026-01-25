using UnityEngine;
using UnityEngine.InputSystem;
using Axiom.Core;
using UnityEngine.EventSystems;
using System;
using System.Reflection;
using System.Globalization;

[DisallowMultipleComponent]
public class LunarReckoning : MonoBehaviour
{
	[Header("Setup")]
	[SerializeField] private Moonfall moonfallPrefab;
	[SerializeField] private Transform aimCamera; // used for screen-to-world ray

	[Header("Targeting")]
	[SerializeField] private LayerMask groundMask = -1; // layers considered as ground
	[SerializeField] private float maxTargetDistance = 60f;
	[SerializeField] private LineRenderer indicatorPrefab;

	private LineRenderer activeIndicator;

	[Header("Damage")]
	[SerializeField] private int baseDamage = 0;
	[SerializeField] private float focusToDamageFactor = 1f; // 100 Focus (8h sleep) => 100 damage

	// Targeting state
	private bool awaitingGroundSelection;
	private int targetingFrameCount; // skip first frame to avoid button position

	void Awake()
	{
		ResolveAimCamera();
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
		StartTargeting();
	}

	private void StartTargeting()
	{
		// Ensure we have a valid camera after scene reloads
		ResolveAimCamera();
		awaitingGroundSelection = true;
		targetingFrameCount = 0;
	}

	public bool TryCastAtPointer()
	{
		if (moonfallPrefab == null) return false;
		if (ResolveAimCamera() == null) return false;

		Vector3 target;
		if (!TryGetGroundPointFromPointer(out target)) return false;

		SpawnMoonfallAt(target);
		return true;
	}

	void Update()
	{
		if (!awaitingGroundSelection) return;
		
		targetingFrameCount++;
		
		// Skip first few frames to avoid using button tap position
		if (targetingFrameCount <= 2)
		{
			return;
		}
		
		// Check for tap/click release FIRST - if we're spawning, don't touch the indicator
		Vector3 point;
		if (TryGetGroundTap(out point))
		{
			awaitingGroundSelection = false;
			// Don't hide indicator - the moonfall will destroy it on impact
			SpawnMoonfallAt(point);
			return; // Exit early, don't process hover logic
		}
		
		// Check for active pointer input (touch held down, or mouse over game area)
		Vector3 hoverPoint;
		bool hasValidHover = TryGetActivePointerPosition(out hoverPoint);
		
		if (hasValidHover)
		{
			ShowIndicator(hoverPoint);
		}
		else
		{
			// Hide indicator when not actively pointing at valid ground
			HideIndicator();
		}
	}

	/// <summary>
	/// Gets pointer position only when there's ACTIVE input (touch held, or mouse not over UI).
	/// Used for showing the indicator while aiming.
	/// </summary>
	private bool TryGetActivePointerPosition(out Vector3 point)
	{
		point = default;
		Camera cam = ResolveAimCamera();
		if (cam == null)
		{
			return false;
		}

		Vector2 screenPos = Vector2.zero;
		bool hasActiveInput = false;

		// Touch: only when finger is actively pressing (dragging to aim)
		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
		{
			int fingerId = Touchscreen.current.primaryTouch.touchId.ReadValue();
			// Skip if over UI
			if (!IsPointerOverUI(fingerId))
			{
				screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
				hasActiveInput = true;
			}
		}
		// Mouse: when button is held OR just hovering (for desktop preview)
		else if (Mouse.current != null)
		{
			// Only show indicator when mouse button is held for consistency with touch
			if (Mouse.current.leftButton.isPressed)
			{
				if (!IsPointerOverUI(-1))
				{
					screenPos = Mouse.current.position.ReadValue();
					hasActiveInput = true;
				}
			}
		}

		if (!hasActiveInput)
		{
			return false;
		}

		Ray ray = cam.ScreenPointToRay(screenPos);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit, maxTargetDistance, groundMask, QueryTriggerInteraction.Ignore))
		{
			point = hit.point;
			return true;
		}
		return false;
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
		Camera cam = ResolveAimCamera();
		if (cam == null) return false;

		Ray ray = cam.ScreenPointToRay(screenPos);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit, maxTargetDistance, groundMask, QueryTriggerInteraction.Ignore))
		{
			point = hit.point;
			return true;
		}
		return false;
	}

	private bool IsPointerOverUI(int pointerId)
	{
		if (EventSystem.current == null) return false;
		return EventSystem.current.IsPointerOverGameObject(pointerId);
	}

	private Camera ResolveAimCamera()
	{
		if (aimCamera == null)
		{
			if (Camera.main != null)
			{
				aimCamera = Camera.main.transform;
			}
		}

		if (aimCamera == null)
		{
			return null;
		}

		var cam = aimCamera.GetComponent<Camera>();
		if (cam == null && Camera.main != null)
		{
			cam = Camera.main;
			aimCamera = cam.transform;
		}
		return cam;
	}

	private void SpawnMoonfallAt(Vector3 groundPoint)
	{
		if (moonfallPrefab == null)
		{
			Debug.LogWarning("[LunarReckoning] moonfallPrefab is not assigned!");
			return;
		}
		var moonfall = Instantiate(moonfallPrefab);
		moonfall.SetOwner(transform);
		moonfall.InitAtTarget(groundPoint, CalculateDamageFromFocus());
		if (activeIndicator != null)
		{
			moonfall.SetIndicatorToDestroy(activeIndicator.gameObject);
			activeIndicator = null;
		}
	}

	private int CalculateDamageFromFocus()
	{
		float focus = CombatTuning.GetFocus();
		return CombatTuning.CalculateStatScaledDamage(focus, baseDamage, focusToDamageFactor);
	}

	private static float ConvertToFloat(object value)
	{
		if (value == null) return -1f;
		if (value is float f) return f;
		if (value is double d) return (float)d;
		if (value is int i) return i;
		if (value is string s)
		{
			if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
			if (float.TryParse(s, out parsed)) return parsed;
		}
		return -1f;
	}

	private void ShowIndicator(Vector3 center)
	{
		if (indicatorPrefab == null)
		{
			return;
		}
		if (activeIndicator == null)
		{
			activeIndicator = Instantiate(indicatorPrefab);
			activeIndicator.loop = true;
			activeIndicator.useWorldSpace = true;
		}
		activeIndicator.enabled = true;
		float radius = moonfallPrefab != null ? moonfallPrefab.Radius : 3.5f;
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

	private void HideIndicator()
	{
		if (activeIndicator != null)
		{
			activeIndicator.enabled = false;
		}
	}
}
