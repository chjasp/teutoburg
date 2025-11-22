using System.Text;
using Teutoburg.Loot;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal equipment panel. Shows current items per slot and offers Unequip buttons.
/// </summary>
[DisallowMultipleComponent]
public sealed class EquipmentPanelUI : MonoBehaviour
{
    [SerializeField] private Text mainHandText;
    [SerializeField] private Button mainHandUnequipButton;
    [SerializeField] private Text offHandText;
    [SerializeField] private Button offHandUnequipButton;
    [SerializeField] private Text headText;
    [SerializeField] private Button headUnequipButton;
    [SerializeField] private Text chestText;
    [SerializeField] private Button chestUnequipButton;
    [SerializeField] private Text handsText;
    [SerializeField] private Button handsUnequipButton;
    [SerializeField] private Text feetText;
    [SerializeField] private Button feetUnequipButton;

    private Equipment equipment;

    void OnEnable()
    {
        if (equipment != null)
        {
            equipment.OnEquipmentChanged += Refresh;
        }
        Refresh();
    }

    void OnDisable()
    {
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= Refresh;
        }
    }

    public void Initialize(Equipment eq)
    {
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= Refresh;
        }
        equipment = eq;
        if (equipment != null)
        {
            equipment.OnEquipmentChanged += Refresh;
        }
        WireButtons();
        Refresh();
    }

    private void WireButtons()
    {
        if (mainHandUnequipButton != null)
        {
            mainHandUnequipButton.onClick.RemoveAllListeners();
            mainHandUnequipButton.onClick.AddListener(() => TryUnequip(EquipmentSlot.MainHand));
        }
        if (offHandUnequipButton != null)
        {
            offHandUnequipButton.onClick.RemoveAllListeners();
            offHandUnequipButton.onClick.AddListener(() => TryUnequip(EquipmentSlot.OffHand));
        }
        if (headUnequipButton != null)
        {
            headUnequipButton.onClick.RemoveAllListeners();
            headUnequipButton.onClick.AddListener(() => TryUnequip(EquipmentSlot.Head));
        }
        if (chestUnequipButton != null)
        {
            chestUnequipButton.onClick.RemoveAllListeners();
            chestUnequipButton.onClick.AddListener(() => TryUnequip(EquipmentSlot.Chest));
        }
        if (handsUnequipButton != null)
        {
            handsUnequipButton.onClick.RemoveAllListeners();
            handsUnequipButton.onClick.AddListener(() => TryUnequip(EquipmentSlot.Hands));
        }
        if (feetUnequipButton != null)
        {
            feetUnequipButton.onClick.RemoveAllListeners();
            feetUnequipButton.onClick.AddListener(() => TryUnequip(EquipmentSlot.Feet));
        }
    }

    private void TryUnequip(EquipmentSlot slot)
    {
        if (equipment == null) return;
        equipment.Unequip(slot);
        Refresh();
    }

    public void Refresh()
    {
        if (equipment == null)
        {
            SetSlotText(mainHandText, null);
            SetSlotText(offHandText, null);
            SetSlotText(headText, null);
            SetSlotText(chestText, null);
            SetSlotText(handsText, null);
            SetSlotText(feetText, null);
            return;
        }
        SetSlotText(mainHandText, equipment.GetEquipped(EquipmentSlot.MainHand));
        SetSlotText(offHandText, equipment.GetEquipped(EquipmentSlot.OffHand));
        SetSlotText(headText, equipment.GetEquipped(EquipmentSlot.Head));
        SetSlotText(chestText, equipment.GetEquipped(EquipmentSlot.Chest));
        SetSlotText(handsText, equipment.GetEquipped(EquipmentSlot.Hands));
        SetSlotText(feetText, equipment.GetEquipped(EquipmentSlot.Feet));
    }

    private void SetSlotText(Text t, ItemInstance item)
    {
        if (t == null) return;
        if (item == null || item.Definition == null)
        {
            t.text = "-";
            t.color = Color.white;
        }
        else
        {
            t.text = item.Definition.DisplayName;
            t.color = GetRarityColor(item.Definition.Rarity);
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


