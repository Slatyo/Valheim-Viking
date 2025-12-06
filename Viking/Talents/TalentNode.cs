using System.Collections.Generic;
using UnityEngine;

namespace Viking.Talents
{
    /// <summary>
    /// Types of talent nodes in the tree.
    /// </summary>
    public enum TalentNodeType
    {
        /// <summary>Small stat bonuses (+5 Str, +10 HP, etc.)</summary>
        Minor,

        /// <summary>Significant bonuses (+20 Str, +15% damage, etc.)</summary>
        Notable,

        /// <summary>Build-defining keystones with major effects and drawbacks.</summary>
        Keystone,

        /// <summary>Starting point node (one of four).</summary>
        Start
    }

    /// <summary>
    /// A stat modifier granted by a talent node.
    /// </summary>
    public class TalentModifier
    {
        /// <summary>Stat to modify (e.g., "Strength", "MaxHealth", "FireDamage").</summary>
        public string Stat { get; set; }

        /// <summary>Type of modification.</summary>
        public TalentModifierType Type { get; set; }

        /// <summary>Value per rank.</summary>
        public float Value { get; set; }

        public TalentModifier(string stat, TalentModifierType type, float value)
        {
            Stat = stat;
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// Type of stat modification.
    /// </summary>
    public enum TalentModifierType
    {
        /// <summary>Add flat value (e.g., +10 health).</summary>
        Flat,

        /// <summary>Add percentage (e.g., +5% damage).</summary>
        Percent,

        /// <summary>Multiply total (e.g., 1.5x).</summary>
        Multiply
    }

    /// <summary>
    /// Represents a single node in the talent tree.
    /// </summary>
    public class TalentNode
    {
        /// <summary>Unique identifier for this node.</summary>
        public string Id { get; set; }

        /// <summary>Display name (localization key, e.g., "$talent_strength_1").</summary>
        public string Name { get; set; }

        /// <summary>Description (localization key).</summary>
        public string Description { get; set; }

        /// <summary>Node type.</summary>
        public TalentNodeType Type { get; set; }

        /// <summary>Maximum ranks that can be allocated.</summary>
        public int MaxRanks { get; set; } = 1;

        /// <summary>Position in the tree UI (for rendering).</summary>
        public Vector2 Position { get; set; }

        /// <summary>Connected node IDs (nodes you can path to/from).</summary>
        public List<string> Connections { get; set; } = new();

        /// <summary>Stat modifiers granted per rank.</summary>
        public List<TalentModifier> Modifiers { get; set; } = new();

        /// <summary>Ability ID granted when at least 1 rank is allocated.</summary>
        public string GrantsAbility { get; set; } = null;

        /// <summary>Icon name for UI.</summary>
        public string Icon { get; set; } = "default";

        /// <summary>
        /// Get total modifier value at a given rank.
        /// </summary>
        public float GetModifierValue(string stat, int ranks)
        {
            float total = 0f;
            foreach (var mod in Modifiers)
            {
                if (mod.Stat == stat)
                {
                    total += mod.Value * ranks;
                }
            }
            return total;
        }

        /// <summary>
        /// Check if this node grants an ability.
        /// </summary>
        public bool HasAbility => !string.IsNullOrEmpty(GrantsAbility);
    }

    /// <summary>
    /// Starting point definition.
    /// </summary>
    public class StartingPoint
    {
        /// <summary>Unique ID (e.g., "warrior", "archer", "mage", "healer").</summary>
        public string Id { get; set; }

        /// <summary>Display name.</summary>
        public string Name { get; set; }

        /// <summary>Description.</summary>
        public string Description { get; set; }

        /// <summary>Starting node ID in the tree.</summary>
        public string StartNodeId { get; set; }

        /// <summary>Position in selection UI.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Icon for selection.</summary>
        public string Icon { get; set; }
    }
}
