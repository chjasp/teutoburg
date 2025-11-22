using System;
using System.Collections.Generic;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// Simple player inventory storage for picked up items.
    /// UI-light: stores items and emits events; no window rendering here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] private int capacity = 30;
        [SerializeField] private List<ItemInstance> items = new List<ItemInstance>();

        /// <summary>
        /// Fired when an item is successfully added to the inventory.
        /// </summary>
        public event Action<ItemInstance> OnItemAdded;

        /// <summary>
        /// Fired when an item is removed from the inventory.
        /// </summary>
        public event Action<ItemInstance> OnItemRemoved;

        /// <summary>
        /// Current number of items.
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// Max capacity of the inventory. Set 0 or negative for unlimited.
        /// </summary>
        public int Capacity
        {
            get => capacity;
            set => capacity = value;
        }

        /// <summary>
        /// Returns a read-only view of the items.
        /// </summary>
        public IReadOnlyList<ItemInstance> Items => items;

        /// <summary>
        /// Returns all items in the inventory (read-only).
        /// </summary>
        public IReadOnlyList<ItemInstance> GetAllItems()
        {
            return items;
        }

        /// <summary>
        /// Attempts to add an item to the inventory.
        /// Returns true if added, false otherwise (e.g., full).
        /// </summary>
        public bool AddItem(ItemInstance item)
        {
            if (item == null) return false;
            if (capacity > 0 && items.Count >= capacity)
            {
                Debug.LogWarning("Inventory full. Cannot add item.");
                return false;
            }
            items.Add(item);
            OnItemAdded?.Invoke(item);
            Debug.Log($"Picked up: {item.Definition.DisplayName} [{item.Definition.Rarity}]");
            return true;
        }

        /// <summary>
        /// Removes the specified item instance from the inventory.
        /// Returns true if the item was found and removed.
        /// </summary>
        public bool RemoveItem(ItemInstance item)
        {
            if (item == null) return false;
            int idx = items.IndexOf(item);
            if (idx < 0) return false;
            var removed = items[idx];
            items.RemoveAt(idx);
            OnItemRemoved?.Invoke(removed);
            return true;
        }
    }
}


