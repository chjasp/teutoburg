using System;
using UnityEngine;

namespace Axiom.Loot
{
    /// <summary>
    /// Defines a single affix that can roll on an item (e.g., +X% Damage, +Y Armor).
    /// Affixes are ScriptableObjects so designers can author them in the editor and reuse across items.
    /// </summary>
    [CreateAssetMenu(menuName = "Axiom/Loot/Affix Definition", fileName = "AffixDefinition")]
    public sealed class AffixDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = Guid.Empty.ToString();
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;

        [Header("Affix")]
        [SerializeField] private AffixType affixType = AffixType.FlatDamage;
        [Tooltip("Minimum value that can be rolled for this affix.")]
        [SerializeField] private float minValue = 1f;
        [Tooltip("Maximum value that can be rolled for this affix.")]
        [SerializeField] private float maxValue = 5f;
        [Tooltip("If true, the value is interpreted as a percentage (e.g., 10 means 10%).")]
        [SerializeField] private bool isPercent = false;
        [Tooltip("Relative chance for this affix when rolling among possible item affixes.")]
        [SerializeField] private int weight = 1;

        [Header("Restrictions")]
        [Tooltip("Restrict to specific item types. Leave empty to allow all.")]
        [SerializeField] private ItemType[] allowedItemTypes;
        [Tooltip("Restrict to specific equipment slots. Leave empty to allow all.")]
        [SerializeField] private EquipmentSlot[] allowedEquipmentSlots;

        #region Public API

        /// <summary>
        /// Unique identifier for the affix. Keep stable across builds.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Designer-friendly display name of the affix (e.g., +% Damage).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Description used for tooltips and debugging.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// The functional type of this affix (drives stat computation).
        /// </summary>
        public AffixType Type => affixType;

        /// <summary>
        /// Minimum rollable value.
        /// </summary>
        public float MinValue => minValue;

        /// <summary>
        /// Maximum rollable value.
        /// </summary>
        public float MaxValue => maxValue;

        /// <summary>
        /// Whether the rolled value is a percentage value.
        /// </summary>
        public bool IsPercent => isPercent;

        /// <summary>
        /// Relative weight for random selection among the parent item's possible affixes.
        /// </summary>
        public int Weight => Mathf.Max(1, weight);

        /// <summary>
        /// Returns true if this affix is allowed for the provided item/equipment.
        /// </summary>
        public bool IsAllowedFor(ItemType itemType, EquipmentSlot slot)
        {
            bool typeOk = allowedItemTypes == null || allowedItemTypes.Length == 0;
            if (!typeOk)
            {
                for (int i = 0; i < allowedItemTypes.Length; i++)
                {
                    if (allowedItemTypes[i] == itemType)
                    {
                        typeOk = true;
                        break;
                    }
                }
            }

            bool slotOk = allowedEquipmentSlots == null || allowedEquipmentSlots.Length == 0;
            if (!slotOk)
            {
                for (int i = 0; i < allowedEquipmentSlots.Length; i++)
                {
                    if (allowedEquipmentSlots[i] == slot)
                    {
                        slotOk = true;
                        break;
                    }
                }
            }

            return typeOk && slotOk;
        }

        /// <summary>
        /// Roll a random value within [MinValue, MaxValue].
        /// </summary>
        public float RollValue()
        {
            return UnityEngine.Random.Range(minValue, maxValue);
        }

        #endregion
    }
}


