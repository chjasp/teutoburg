using System;
using UnityEngine;

namespace Axiom.Loot
{
    /// <summary>
    /// Defines the high-level type of an item (drives base stats semantics).
    /// </summary>
    public enum ItemType
    {
        Weapon = 0,
        Armor = 1,
        Ring = 2,
        Amulet = 3,
        Shield = 4,
        Helmet = 5,
        Boots = 6,
        Gloves = 7,
        Belt = 8,
        Offhand = 9,
        Consumable = 10
    }

    /// <summary>
    /// Equipment slots that items can be equipped into.
    /// </summary>
    public enum EquipmentSlot
    {
        MainHand = 0,
        OffHand = 1,
        Head = 2,
        Chest = 3,
        Legs = 4,
        Feet = 5,
        Hands = 6,
        Finger = 7,
        Neck = 8,
        Waist = 9
    }

    /// <summary>
    /// Rarity tiers for items. Used to influence drop rates and affix counts.
    /// </summary>
    public enum Rarity
    {
        Common = 0,
        Magic = 1,
        Rare = 2,
        Legendary = 3
    }

    /// <summary>
    /// Types of affix effects. Item stats computations interpret these.
    /// </summary>
    public enum AffixType
    {
        FlatDamage = 0,
        PercentDamage = 1,
        FlatArmor = 2,
        PercentArmor = 3,
        FlatLife = 4,
        PercentLife = 5
    }
}


