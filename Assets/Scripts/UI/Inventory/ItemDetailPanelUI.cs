using System;
using System.Text;
using Axiom.Loot;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple item detail panel: shows name, icon, rarity, stats, and equip/unequip actions.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemDetailPanelUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text rarityText;
    [SerializeField] private Text statsText;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button closeButton;

    private PlayerInventory inventory;
    private Equipment equipment;
    private ItemInstance item;
    private Action onChanged;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
        Hide();
    }

    public void Show(PlayerInventory inv, Equipment eq, ItemInstance it, Action onChangedCallback)
    {
        inventory = inv;
        equipment = eq;
        item = it;
        onChanged = onChangedCallback;
        if (root != null) root.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        inventory = null;
        equipment = null;
        item = null;
        onChanged = null;
    }

    private void Refresh()
    {
        if (item == null || item.Definition == null) return;
        if (iconImage != null)
        {
            iconImage.sprite = item.Definition.Icon;
            iconImage.enabled = iconImage.sprite != null;
        }
        if (nameText != null)
        {
            nameText.text = item.Definition.DisplayName;
            nameText.color = GetRarityColor(item.Definition.Rarity);
        }
        if (rarityText != null)
        {
            rarityText.text = item.Definition.Rarity.ToString();
            rarityText.color = GetRarityColor(item.Definition.Rarity);
        }
        if (statsText != null)
        {
            statsText.text = BuildStatsText(item);
        }

        bool isEquipped = (equipment != null) && equipment.IsEquipped(item);
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(!isEquipped);
            equipButton.onClick.RemoveAllListeners();
            if (equipment != null && inventory != null && equipment.CanEquip(item))
            {
                equipButton.onClick.AddListener(() =>
                {
                    if (equipment.Equip(item))
                    {
                        onChanged?.Invoke();
                        Refresh();
                    }
                });
            }
            else
            {
                equipButton.interactable = false;
            }
        }
        if (unequipButton != null)
        {
            unequipButton.gameObject.SetActive(isEquipped);
            unequipButton.onClick.RemoveAllListeners();
            if (isEquipped && equipment != null && inventory != null)
            {
                unequipButton.onClick.AddListener(() =>
                {
                    var slot = item.Definition.AllowedEquipmentSlot;
                    if (equipment.Unequip(slot))
                    {
                        onChanged?.Invoke();
                        Refresh();
                    }
                });
            }
        }
    }

    private string BuildStatsText(ItemInstance it)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Type: {it.Definition.ItemType}");
        switch (it.Definition.ItemType)
        {
            case ItemType.Weapon:
                it.GetTotalDamageRange(out int min, out int max);
                sb.AppendLine($"Damage: {min}-{max}");
                break;
            case ItemType.Armor:
                sb.AppendLine($"Armor: {it.GetArmorBonus()}");
                break;
            default:
                // Could show generic stats or affixes here in the future
                break;
        }
        return sb.ToString();
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


