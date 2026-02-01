using System.Collections.Generic;
using Axiom.Loot;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-level controller for the Inventory & Equipment UI. Populates list and hooks details/equip actions.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private Equipment equipment;

    [Header("List")]
    [SerializeField] private RectTransform listContainer;
    [SerializeField] private InventoryItemEntryUI itemEntryPrefab;

    [Header("Panels")]
    [SerializeField] private ItemDetailPanelUI detailPanel;
    [SerializeField] private EquipmentPanelUI equipmentPanel;
    [SerializeField] private GameObject[] hideWhileOpen;

    void Awake()
    {
        if (playerInventory == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerInventory = player.GetComponentInParent<PlayerInventory>();
        }
        if (equipment == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) equipment = player.GetComponentInParent<Equipment>();
        }
    }

    void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnItemAdded += HandleInventoryChanged;
            playerInventory.OnItemRemoved += HandleInventoryChanged;
        }
        if (equipmentPanel != null && equipment != null)
        {
            equipmentPanel.Initialize(equipment);
        }
        RefreshList();
        SetHiddenObjects(true);
    }

    void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnItemAdded -= HandleInventoryChanged;
            playerInventory.OnItemRemoved -= HandleInventoryChanged;
        }
        SetHiddenObjects(false);
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

    private void HandleInventoryChanged(ItemInstance _)
    {
        RefreshList();
    }

    public void RefreshList()
    {
        if (listContainer == null || itemEntryPrefab == null || playerInventory == null) return;
        // clear old entries
        for (int i = listContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(listContainer.GetChild(i).gameObject);
        }
        var items = playerInventory.GetAllItems();
        for (int i = 0; i < items.Count; i++)
        {
            var entry = Instantiate(itemEntryPrefab, listContainer);
            var item = items[i];
            bool isEquipped = equipment != null && equipment.IsEquipped(item);
            entry.Bind(item, isEquipped, OnItemTapped);
        }
    }

    private void OnItemTapped(ItemInstance item)
    {
        if (detailPanel == null || playerInventory == null || equipment == null) return;
        detailPanel.Show(playerInventory, equipment, item, () =>
        {
            RefreshList();
            if (equipmentPanel != null) equipmentPanel.Refresh();
        });
    }
}

