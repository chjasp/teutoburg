using System;
using System.Collections.Generic;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// Manages equipped items per equipment slot, and exposes aggregated stat APIs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class Equipment : MonoBehaviour
    {
        [Header("Slots")]
        [SerializeField] private ItemInstance mainHand;
        [SerializeField] private ItemInstance offHand;
        [SerializeField] private ItemInstance head;
        [SerializeField] private ItemInstance chest;
        [SerializeField] private ItemInstance hands;
        [SerializeField] private ItemInstance feet;

        private PlayerInventory inventory;

        /// <summary>
        /// Fired when any slot's item changes (equip/unequip).
        /// </summary>
        public event Action OnEquipmentChanged;

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        #region Query

        /// <summary>
        /// Returns the equipped item in a given slot (or null).
        /// </summary>
        public ItemInstance GetEquipped(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand: return mainHand;
                case EquipmentSlot.OffHand: return offHand;
                case EquipmentSlot.Head: return head;
                case EquipmentSlot.Chest: return chest;
                case EquipmentSlot.Hands: return hands;
                case EquipmentSlot.Feet: return feet;
                default: return null;
            }
        }

        /// <summary>
        /// Returns true if the given item is currently equipped in any slot.
        /// </summary>
        public bool IsEquipped(ItemInstance item)
        {
            if (item == null) return false;
            return mainHand == item || offHand == item || head == item || chest == item || hands == item || feet == item;
        }

        /// <summary>
        /// Checks if the given item can be equipped (slot compatibility).
        /// </summary>
        public bool CanEquip(ItemInstance item)
        {
            if (item == null || item.Definition == null) return false;
            var slot = item.Definition.AllowedEquipmentSlot;
            // Limit to slots we manage
            switch (slot)
            {
                case EquipmentSlot.MainHand:
                case EquipmentSlot.OffHand:
                case EquipmentSlot.Head:
                case EquipmentSlot.Chest:
                case EquipmentSlot.Hands:
                case EquipmentSlot.Feet:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Equip / Unequip

        /// <summary>
        /// Equips the provided item into its allowed slot.
        /// If the slot is occupied, the old item is returned to inventory.
        /// The provided item is removed from inventory on success.
        /// </summary>
        public bool Equip(ItemInstance item)
        {
            if (!CanEquip(item)) return false;
            if (inventory == null) return false;
            if (!inventory.RemoveItem(item)) return false; // ensure it's in inventory, then remove

            var slot = item.Definition.AllowedEquipmentSlot;
            var displaced = SetSlot(slot, item);
            if (displaced != null)
            {
                inventory.AddItem(displaced);
            }
            OnEquipmentChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Unequips whatever is in the specified slot and returns it to inventory.
        /// </summary>
        public bool Unequip(EquipmentSlot slot)
        {
            if (inventory == null) return false;
            var current = GetEquipped(slot);
            if (current == null) return false;
            SetSlot(slot, null);
            bool added = inventory.AddItem(current);
            OnEquipmentChanged?.Invoke();
            return added;
        }

        private ItemInstance SetSlot(EquipmentSlot slot, ItemInstance item)
        {
            ItemInstance displaced = null;
            switch (slot)
            {
                case EquipmentSlot.MainHand:
                    displaced = mainHand;
                    mainHand = item;
                    break;
                case EquipmentSlot.OffHand:
                    displaced = offHand;
                    offHand = item;
                    break;
                case EquipmentSlot.Head:
                    displaced = head;
                    head = item;
                    break;
                case EquipmentSlot.Chest:
                    displaced = chest;
                    chest = item;
                    break;
                case EquipmentSlot.Hands:
                    displaced = hands;
                    hands = item;
                    break;
                case EquipmentSlot.Feet:
                    displaced = feet;
                    feet = item;
                    break;
                default:
                    // Unsupported slot ignored and item returned to inventory by caller (but should be prevented by CanEquip)
                    break;
            }
            return displaced;
        }

        #endregion

        #region Aggregated Stats

        /// <summary>
        /// Calculates the total armor from all equipped items.
        /// </summary>
        public int GetTotalArmor()
        {
            int armor = 0;
            if (head != null) armor += head.GetArmorBonus();
            if (chest != null) armor += chest.GetArmorBonus();
            if (hands != null) armor += hands.GetArmorBonus();
            if (feet != null) armor += feet.GetArmorBonus();
            if (offHand != null) armor += offHand.GetArmorBonus();
            return armor;
        }

        /// <summary>
        /// Returns the weapon damage range from the main hand item if present; otherwise (0,0).
        /// </summary>
        public void GetWeaponDamageRange(out int minDamage, out int maxDamage)
        {
            if (mainHand != null)
            {
                mainHand.GetTotalDamageRange(out minDamage, out maxDamage);
                return;
            }
            minDamage = 0;
            maxDamage = 0;
        }

        /// <summary>
        /// Returns all currently equipped items.
        /// </summary>
        public IEnumerable<ItemInstance> GetAllEquipped()
        {
            if (mainHand != null) yield return mainHand;
            if (offHand != null) yield return offHand;
            if (head != null) yield return head;
            if (chest != null) yield return chest;
            if (hands != null) yield return hands;
            if (feet != null) yield return feet;
        }

        #endregion
    }
}


