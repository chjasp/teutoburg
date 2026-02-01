using UnityEngine;

/// <summary>
/// Simple toggle controller for the Inventory panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryPanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private bool closeOnStart = true;
    [SerializeField] private GameObject[] hideWhileOpen;

    private void Start()
    {
        if (closeOnStart)
        {
            Close();
        }
    }

    public void Open()
    {
        SetPanelActive(true);
    }

    public void Close()
    {
        SetPanelActive(false);
    }

    public void Toggle()
    {
        if (inventoryPanel == null) return;
        inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    }

    private void SetPanelActive(bool active)
    {
        if (inventoryPanel == null) return;
        inventoryPanel.SetActive(active);
        SetHiddenObjects(active);
    }

    private void SetHiddenObjects(bool inventoryOpen)
    {
        if (hideWhileOpen == null) return;
        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            var go = hideWhileOpen[i];
            if (go == null) continue;
            go.SetActive(!inventoryOpen);
        }
    }
}
