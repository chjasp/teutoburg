using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        if (EventSystem.current == null)
        {
            Debug.Log("[UIRaycastDebugger] EventSystem.current is null - exiting Update");
            return;
        }

        bool pressedThisFrame = false;
        Vector2 pointerPosition = default;

        if (Mouse.current != null)
        {
            pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            pointerPosition = Mouse.current.position.ReadValue();
            Debug.Log($"[UIRaycastDebugger] Mouse detected. leftPressedThisFrame={pressedThisFrame}, pos={pointerPosition}");
        }
        else if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            pressedThisFrame = touch.press.wasPressedThisFrame;
            pointerPosition = touch.position.ReadValue();
            Debug.Log($"[UIRaycastDebugger] Touchscreen detected. pressedThisFrame={pressedThisFrame}, pos={pointerPosition}");
        }
        else
        {
            Debug.Log("[UIRaycastDebugger] No Mouse or Touchscreen device detected");
        }

        if (!pressedThisFrame)
        {
            Debug.Log("[UIRaycastDebugger] No press this frame - exiting Update");
            return;
        }

        var data = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition
        };
        var results = new List<RaycastResult>();
        Debug.Log($"[UIRaycastDebugger] Performing UI raycast at screen position {pointerPosition}");
        EventSystem.current.RaycastAll(data, results);

        if (results.Count == 0)
            Debug.Log("[UIRaycastDebugger] No UI hit (0 results)");
        else
            Debug.Log("[UIRaycastDebugger] " + results.Count + " hit(s) (top first): " + string.Join(" > ", results.Select(r => r.gameObject.name)));
    }
}
