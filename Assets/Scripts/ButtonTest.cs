using UnityEngine;
using UnityEngine.EventSystems;

public class UiClickProbe : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData e)
    {
        Debug.Log($"[UiClickProbe] Click on {name} (pointerId={e.pointerId}) " +
                  $"top={e.pointerCurrentRaycast.gameObject?.name}");
    }
}