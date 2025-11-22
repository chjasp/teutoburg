using System;
using UnityEngine;

namespace Teutoburg.Loot
{
    /// <summary>
    /// World representation of a dropped item. Holds an ItemInstance and handles pickup.
    /// Requires a trigger collider (and ideally a kinematic Rigidbody) on the same GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldItemPickup : MonoBehaviour
    {
        [Header("Visuals (optional)")]
        [SerializeField] private SpriteRenderer iconRenderer; // optional
        [SerializeField] private TextMesh nameLabel;          // optional world-space label
        [SerializeField] private Color commonColor = Color.white;
        [SerializeField] private Color magicColor = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private Color rareColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.5f, 0.1f);

        [Header("Pickup")]
        [Tooltip("If true, destroys this pickup immediately after a successful pickup.")]
        [SerializeField] private bool destroyOnPickup = true;
        [Tooltip("Time before destroying after pickup (if destroyOnPickup).")]
        [SerializeField] private float destroyDelay = 0f;

        [SerializeField] private ItemInstance item;

        /// <summary>
        /// The item represented by this pickup.
        /// </summary>
        public ItemInstance Item => item;

        /// <summary>
        /// Fired when this pickup is collected successfully.
        /// </summary>
        public event Action<ItemInstance> OnPickedUp;

        /// <summary>
        /// Initializes the pickup with the given item instance and updates visuals.
        /// </summary>
        public void Initialize(ItemInstance instance)
        {
            item = instance;
            RefreshVisuals();
        }

        void LateUpdate()
        {
            // Make the label face the camera for readability
            if (nameLabel != null && Camera.main != null)
            {
                var cam = Camera.main;
                Vector3 dir = (nameLabel.transform.position - cam.transform.position).normalized;
                nameLabel.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (item == null) return;
            // Only allow the Player to pick up
            if (!other.CompareTag("Player")) return;

            var inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            if (inventory.AddItem(item))
            {
                OnPickedUp?.Invoke(item);
                if (destroyOnPickup)
                {
                    Destroy(gameObject, destroyDelay);
                }
            }
        }

        void OnMouseDown()
        {
            // Simple tap/click pickup path for testing; requires collider and raycaster from Camera
            if (item == null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var inventory = player.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;
            if (inventory.AddItem(item))
            {
                OnPickedUp?.Invoke(item);
                if (destroyOnPickup)
                {
                    Destroy(gameObject, destroyDelay);
                }
            }
        }

        private void RefreshVisuals()
        {
            if (item == null || item.Definition == null) return;
            // Icon
            if (iconRenderer != null)
            {
                iconRenderer.sprite = item.Definition.Icon;
            }
            // Label text and color
            if (nameLabel != null)
            {
                nameLabel.text = item.Definition.DisplayName;
                nameLabel.color = GetRarityColor(item.Definition.Rarity);
            }
        }

        private Color GetRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return commonColor;
                case Rarity.Magic: return magicColor;
                case Rarity.Rare: return rareColor;
                case Rarity.Legendary: return legendaryColor;
                default: return commonColor;
            }
        }
    }
}


