using System;
using Teutoburg.Loot;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI row representing one inventory item. Displays name, rarity color, optional icon.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryItemEntryUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text nameText;
    [SerializeField] private Image iconImage;

    private ItemInstance boundItem;
    private Action<ItemInstance> onClicked;

    public void Bind(ItemInstance item, bool isEquipped, Action<ItemInstance> onClick)
    {
        boundItem = item;
        onClicked = onClick;
        if (nameText != null && item != null && item.Definition != null)
        {
            string equippedMarker = isEquipped ? "★ " : "";
            nameText.text = equippedMarker + item.Definition.DisplayName;
            nameText.color = GetRarityColor(item.Definition.Rarity);
        }
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Definition.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (boundItem != null && onClicked != null) onClicked(boundItem);
            });
        }
    }

    private Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return Color.white;
            case Rarity.Magic: return new Color(0.3f, 0.5f, 1f);
            case Rarity.Rare: return new Color(1f, 0.85f, 0.3f);
            case Rarity.Legendary: return new Color(1f, 0.5f, 0.1f);
            default: return Color.white;
        }
    }
}


