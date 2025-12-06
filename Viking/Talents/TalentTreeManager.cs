using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Viking.Data;

namespace Viking.Talents
{
    /// <summary>
    /// Manages the talent tree structure and provides access to nodes.
    /// The tree is designed as a web spreading OUTWARD from 4 starting points,
    /// with paths connecting between class areas for hybrid builds.
    /// </summary>
    public static class TalentTreeManager
    {
        private static Dictionary<string, TalentNode> _nodes = new();
        private static Dictionary<string, StartingPoint> _startingPoints = new();
        private static bool _initialized = false;

        // Tree layout constants
        private const float RING_SPACING = 45f;      // Distance between rings
        private const float NODE_SPACING = 35f;      // Angular spacing multiplier
        private const float CENTER_RADIUS = 80f;     // Radius where start nodes sit

        /// <summary>
        /// Initialize the talent tree with all nodes.
        /// </summary>
        internal static void Initialize()
        {
            if (_initialized) return;

            CreateStartingPoints();
            CreateTalentTree();

            _initialized = true;
            Plugin.Log.LogInfo($"Talent tree initialized with {_nodes.Count} nodes and {_startingPoints.Count} starting points");
        }

        #region Node Access

        public static TalentNode GetNode(string nodeId)
        {
            return _nodes.TryGetValue(nodeId, out var node) ? node : null;
        }

        public static bool HasNode(string nodeId)
        {
            return _nodes.ContainsKey(nodeId);
        }

        public static IEnumerable<TalentNode> GetAllNodes()
        {
            return _nodes.Values;
        }

        public static IEnumerable<TalentNode> GetNodesByType(TalentNodeType type)
        {
            return _nodes.Values.Where(n => n.Type == type);
        }

        public static StartingPoint GetStartingPoint(string id)
        {
            return _startingPoints.TryGetValue(id, out var sp) ? sp : null;
        }

        public static IEnumerable<StartingPoint> GetAllStartingPoints()
        {
            return _startingPoints.Values;
        }

        #endregion

        #region Path Validation

        public static bool IsNodeReachable(VikingPlayerData data, string nodeId)
        {
            if (data == null) return false;

            var node = GetNode(nodeId);
            if (node == null) return false;

            if (node.Type == TalentNodeType.Start)
            {
                var startPoint = _startingPoints.Values.FirstOrDefault(sp => sp.StartNodeId == nodeId);
                return startPoint != null && (string.IsNullOrEmpty(data.StartingPoint) || data.StartingPoint == startPoint.Id);
            }

            if (string.IsNullOrEmpty(data.StartingPoint))
            {
                return false;
            }

            foreach (var connection in node.Connections)
            {
                if (data.HasNode(connection))
                {
                    return true;
                }
            }

            foreach (var allocatedNodeId in data.AllocatedNodes.Keys)
            {
                var allocatedNode = GetNode(allocatedNodeId);
                if (allocatedNode != null && allocatedNode.Connections.Contains(nodeId))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanDeallocateNode(VikingPlayerData data, string nodeId)
        {
            if (data == null) return false;
            if (!data.HasNode(nodeId)) return false;

            if (data.GetNodeRanks(nodeId) > 1) return true;

            var simulatedNodes = new HashSet<string>(data.AllocatedNodes.Keys);
            simulatedNodes.Remove(nodeId);

            if (simulatedNodes.Count == 0) return true;

            var startPoint = GetStartingPoint(data.StartingPoint);
            if (startPoint == null) return false;

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startPoint.StartNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);

                var currentNode = GetNode(current);
                if (currentNode == null) continue;

                foreach (var conn in currentNode.Connections)
                {
                    if (simulatedNodes.Contains(conn) && !visited.Contains(conn))
                    {
                        queue.Enqueue(conn);
                    }
                }
            }

            return simulatedNodes.All(n => visited.Contains(n) || n == startPoint.StartNodeId);
        }

        #endregion

        #region Tree Definition

        private static void CreateStartingPoints()
        {
            // Starting points are at cardinal directions, facing OUTWARD
            // Players expand outward from their starting point

            _startingPoints["warrior"] = new StartingPoint
            {
                Id = "warrior",
                Name = "Warrior",
                Description = "Masters of melee combat, strength, and endurance",
                StartNodeId = "start_warrior",
                Position = new Vector2(0, -CENTER_RADIUS),
                Icon = "warrior"
            };

            _startingPoints["ranger"] = new StartingPoint
            {
                Id = "ranger",
                Name = "Ranger",
                Description = "Swift hunters skilled in ranged combat and evasion",
                StartNodeId = "start_ranger",
                Position = new Vector2(-CENTER_RADIUS, 0),
                Icon = "ranger"
            };

            _startingPoints["sorcerer"] = new StartingPoint
            {
                Id = "sorcerer",
                Name = "Sorcerer",
                Description = "Wielders of elemental magic and arcane power",
                StartNodeId = "start_sorcerer",
                Position = new Vector2(CENTER_RADIUS, 0),
                Icon = "sorcerer"
            };

            _startingPoints["guardian"] = new StartingPoint
            {
                Id = "guardian",
                Name = "Guardian",
                Description = "Protectors who heal allies and ward against harm",
                StartNodeId = "start_guardian",
                Position = new Vector2(0, CENTER_RADIUS),
                Icon = "guardian"
            };
        }

        private static void CreateTalentTree()
        {
            // Create starting nodes
            CreateStartNodes();

            // Create each class's branch (expanding OUTWARD)
            CreateWarriorBranch();  // Bottom (South) - expands down
            CreateRangerBranch();   // Left (West) - expands left
            CreateSorcererBranch(); // Right (East) - expands right
            CreateGuardianBranch(); // Top (North) - expands up

            // Create connecting paths between classes
            CreateBridgeNodes();

            // Create central defensive ring (accessible from all starts)
            CreateDefensiveRing();
        }

        private static void CreateStartNodes()
        {
            AddNode(new TalentNode
            {
                Id = "start_warrior",
                Name = "Warrior Origin",
                Description = "The path of blood and steel begins here",
                Type = TalentNodeType.Start,
                Position = new Vector2(0, -CENTER_RADIUS),
                Connections = new List<string> { "w_str_1", "w_vit_1", "w_melee_1", "def_ring_s" }
            });

            AddNode(new TalentNode
            {
                Id = "start_ranger",
                Name = "Ranger Origin",
                Description = "The hunter's instinct awakens",
                Type = TalentNodeType.Start,
                Position = new Vector2(-CENTER_RADIUS, 0),
                Connections = new List<string> { "r_dex_1", "r_speed_1", "r_ranged_1", "def_ring_w" }
            });

            AddNode(new TalentNode
            {
                Id = "start_sorcerer",
                Name = "Sorcerer Origin",
                Description = "Arcane energies flow through you",
                Type = TalentNodeType.Start,
                Position = new Vector2(CENTER_RADIUS, 0),
                Connections = new List<string> { "s_int_1", "s_eitr_1", "s_elem_1", "def_ring_e" }
            });

            AddNode(new TalentNode
            {
                Id = "start_guardian",
                Name = "Guardian Origin",
                Description = "The duty to protect guides your path",
                Type = TalentNodeType.Start,
                Position = new Vector2(0, CENTER_RADIUS),
                Connections = new List<string> { "g_spirit_1", "g_healing_1", "g_ward_1", "def_ring_n" }
            });
        }

        #region Warrior Branch (South - expands downward/outward)

        private static void CreateWarriorBranch()
        {
            float baseY = -CENTER_RADIUS;

            // === STRENGTH PATH (South-West) ===
            CreateStrengthPath(baseY);

            // === VITALITY PATH (South-East) ===
            CreateVitalityPath(baseY);

            // === MELEE MASTERY PATH (Center-South) ===
            CreateMeleePath(baseY);

            // === WARRIOR DEFENSE PATH ===
            CreateWarriorDefensePath(baseY);

            // === WARRIOR KEYSTONES ===
            CreateWarriorKeystones(baseY);
        }

        private static void CreateStrengthPath(float baseY)
        {
            // Ring 1
            AddMinorNode("w_str_1", "Strength I", "+3 Strength", new Vector2(-40, baseY - 35),
                new[] { "start_warrior", "w_str_2", "w_melee_1" },
                new TalentModifier("Strength", TalentModifierType.Flat, 3f), 5);

            // Ring 2
            AddMinorNode("w_str_2", "Strength II", "+3 Strength", new Vector2(-55, baseY - 65),
                new[] { "w_str_1", "w_str_3", "w_phys_1" },
                new TalentModifier("Strength", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("w_phys_1", "Physical Power I", "+2% Physical Damage", new Vector2(-80, baseY - 55),
                new[] { "w_str_2", "w_phys_2" },
                new TalentModifier("PhysicalDamage", TalentModifierType.Percent, 2f), 5);

            // Ring 3
            AddNotableNode("w_str_3", "Might", "+10 Strength, +5% Physical Damage", new Vector2(-70, baseY - 95),
                new[] { "w_str_2", "w_str_4", "w_carry_1" },
                new[] {
                    new TalentModifier("Strength", TalentModifierType.Flat, 10f),
                    new TalentModifier("PhysicalDamage", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("w_phys_2", "Physical Power II", "+2% Physical Damage", new Vector2(-105, baseY - 75),
                new[] { "w_phys_1", "w_phys_3" },
                new TalentModifier("PhysicalDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("w_carry_1", "Pack Mule I", "+25 Carry Weight", new Vector2(-45, baseY - 110),
                new[] { "w_str_3", "w_carry_2" },
                new TalentModifier("CarryWeight", TalentModifierType.Flat, 25f), 3);

            // Ring 4
            AddMinorNode("w_str_4", "Strength III", "+3 Strength", new Vector2(-85, baseY - 125),
                new[] { "w_str_3", "w_str_5", "w_stagger_1" },
                new TalentModifier("Strength", TalentModifierType.Flat, 3f), 5);

            AddNotableNode("w_phys_3", "Brutality", "+8% Physical Damage, +5% Attack Speed", new Vector2(-120, baseY - 100),
                new[] { "w_phys_2", "w_blunt_1", "w_slash_1" },
                new[] {
                    new TalentModifier("PhysicalDamage", TalentModifierType.Percent, 8f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("w_carry_2", "Pack Mule II", "+25 Carry Weight", new Vector2(-30, baseY - 140),
                new[] { "w_carry_1" },
                new TalentModifier("CarryWeight", TalentModifierType.Flat, 25f), 3);

            // Ring 5 - Specialization
            AddMinorNode("w_str_5", "Strength IV", "+3 Strength", new Vector2(-100, baseY - 155),
                new[] { "w_str_4", "w_str_notable" },
                new TalentModifier("Strength", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("w_stagger_1", "Staggering Blows I", "+5% Stagger Damage", new Vector2(-70, baseY - 145),
                new[] { "w_str_4", "w_stagger_2" },
                new TalentModifier("StaggerDamage", TalentModifierType.Percent, 5f), 3);

            AddMinorNode("w_blunt_1", "Blunt Mastery I", "+5% Blunt Damage", new Vector2(-145, baseY - 115),
                new[] { "w_phys_3", "w_blunt_2" },
                new TalentModifier("BluntDamage", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("w_slash_1", "Slash Mastery I", "+5% Slash Damage", new Vector2(-135, baseY - 85),
                new[] { "w_phys_3", "w_slash_2" },
                new TalentModifier("SlashDamage", TalentModifierType.Percent, 5f), 5);

            // Ring 6 - Deep nodes
            AddNotableNode("w_str_notable", "Titan's Strength", "+15 Strength, +3% Max Health", new Vector2(-115, baseY - 185),
                new[] { "w_str_5", "key_berserker" },
                new[] {
                    new TalentModifier("Strength", TalentModifierType.Flat, 15f),
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 3f)
                });

            AddNotableNode("w_stagger_2", "Bone Crusher", "+10% Stagger Damage, enemies stay staggered longer", new Vector2(-80, baseY - 175),
                new[] { "w_stagger_1" },
                new[] {
                    new TalentModifier("StaggerDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("StaggerDuration", TalentModifierType.Percent, 20f)
                });

            AddMinorNode("w_blunt_2", "Blunt Mastery II", "+5% Blunt Damage", new Vector2(-165, baseY - 140),
                new[] { "w_blunt_1", "w_blunt_notable" },
                new TalentModifier("BluntDamage", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("w_slash_2", "Slash Mastery II", "+5% Slash Damage", new Vector2(-155, baseY - 100),
                new[] { "w_slash_1", "w_slash_notable" },
                new TalentModifier("SlashDamage", TalentModifierType.Percent, 5f), 5);

            // Ring 7 - Weapon notables
            AddNotableNode("w_blunt_notable", "Skull Splitter", "+15% Blunt Damage, +10% Stagger vs Armored", new Vector2(-185, baseY - 165),
                new[] { "w_blunt_2" },
                new[] {
                    new TalentModifier("BluntDamage", TalentModifierType.Percent, 15f),
                    new TalentModifier("StaggerVsArmored", TalentModifierType.Percent, 10f)
                });

            AddNotableNode("w_slash_notable", "Blade Dancer", "+15% Slash Damage, +8% Attack Speed with Swords", new Vector2(-175, baseY - 115),
                new[] { "w_slash_2" },
                new[] {
                    new TalentModifier("SlashDamage", TalentModifierType.Percent, 15f),
                    new TalentModifier("SwordAttackSpeed", TalentModifierType.Percent, 8f)
                });
        }

        private static void CreateVitalityPath(float baseY)
        {
            // Ring 1
            AddMinorNode("w_vit_1", "Vitality I", "+8 Max Health", new Vector2(40, baseY - 35),
                new[] { "start_warrior", "w_vit_2", "w_melee_1" },
                new TalentModifier("MaxHealth", TalentModifierType.Flat, 8f), 5);

            // Ring 2
            AddMinorNode("w_vit_2", "Vitality II", "+8 Max Health", new Vector2(55, baseY - 65),
                new[] { "w_vit_1", "w_vit_3", "w_regen_1" },
                new TalentModifier("MaxHealth", TalentModifierType.Flat, 8f), 5);

            AddMinorNode("w_regen_1", "Recovery I", "+5% Health Regen", new Vector2(80, baseY - 55),
                new[] { "w_vit_2", "w_regen_2" },
                new TalentModifier("HealthRegen", TalentModifierType.Percent, 5f), 5);

            // Ring 3
            AddNotableNode("w_vit_3", "Fortitude", "+20 Max Health, +5% Max Health", new Vector2(70, baseY - 95),
                new[] { "w_vit_2", "w_vit_4", "w_armor_path_1" },
                new[] {
                    new TalentModifier("MaxHealth", TalentModifierType.Flat, 20f),
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("w_regen_2", "Recovery II", "+5% Health Regen", new Vector2(105, baseY - 75),
                new[] { "w_regen_1", "w_regen_3" },
                new TalentModifier("HealthRegen", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("w_armor_path_1", "Thick Skin I", "+5 Armor", new Vector2(45, baseY - 110),
                new[] { "w_vit_3", "w_armor_path_2" },
                new TalentModifier("Armor", TalentModifierType.Flat, 5f), 5);

            // Ring 4
            AddMinorNode("w_vit_4", "Vitality III", "+8 Max Health", new Vector2(85, baseY - 125),
                new[] { "w_vit_3", "w_vit_5", "w_block_1" },
                new TalentModifier("MaxHealth", TalentModifierType.Flat, 8f), 5);

            AddNotableNode("w_regen_3", "Rapid Recovery", "+10% Health Regen, +5% Stamina Regen", new Vector2(120, baseY - 100),
                new[] { "w_regen_2", "w_leech_1" },
                new[] {
                    new TalentModifier("HealthRegen", TalentModifierType.Percent, 10f),
                    new TalentModifier("StaminaRegen", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("w_armor_path_2", "Thick Skin II", "+5 Armor", new Vector2(30, baseY - 140),
                new[] { "w_armor_path_1", "w_armor_notable" },
                new TalentModifier("Armor", TalentModifierType.Flat, 5f), 5);

            // Ring 5
            AddMinorNode("w_vit_5", "Vitality IV", "+8 Max Health", new Vector2(100, baseY - 155),
                new[] { "w_vit_4", "w_vit_notable" },
                new TalentModifier("MaxHealth", TalentModifierType.Flat, 8f), 5);

            AddMinorNode("w_block_1", "Shield Wall I", "+8 Block Power", new Vector2(70, baseY - 145),
                new[] { "w_vit_4", "w_block_2" },
                new TalentModifier("BlockPower", TalentModifierType.Flat, 8f), 5);

            AddMinorNode("w_leech_1", "Life Leech I", "+1% Life Steal", new Vector2(145, baseY - 115),
                new[] { "w_regen_3", "w_leech_2" },
                new TalentModifier("LifeSteal", TalentModifierType.Flat, 1f), 3);

            // Ring 6
            AddNotableNode("w_vit_notable", "Ironclad Constitution", "+30 Max Health, +10% Max Health", new Vector2(115, baseY - 185),
                new[] { "w_vit_5", "key_juggernaut" },
                new[] {
                    new TalentModifier("MaxHealth", TalentModifierType.Flat, 30f),
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 10f)
                });

            AddNotableNode("w_block_2", "Unbreakable", "+15 Block Power, +10% Block Efficiency", new Vector2(85, baseY - 175),
                new[] { "w_block_1" },
                new[] {
                    new TalentModifier("BlockPower", TalentModifierType.Flat, 15f),
                    new TalentModifier("BlockEfficiency", TalentModifierType.Percent, 10f)
                });

            AddMinorNode("w_leech_2", "Life Leech II", "+1% Life Steal", new Vector2(165, baseY - 140),
                new[] { "w_leech_1", "w_leech_notable" },
                new TalentModifier("LifeSteal", TalentModifierType.Flat, 1f), 3);

            AddNotableNode("w_armor_notable", "Living Fortress", "+15 Armor, +5% Physical Resistance", new Vector2(15, baseY - 170),
                new[] { "w_armor_path_2" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 15f),
                    new TalentModifier("PhysicalResist", TalentModifierType.Percent, 5f)
                });

            AddNotableNode("w_leech_notable", "Vampiric Strikes", "+3% Life Steal, heal for 5% of overkill damage", new Vector2(185, baseY - 165),
                new[] { "w_leech_2" },
                new[] {
                    new TalentModifier("LifeSteal", TalentModifierType.Flat, 3f),
                    new TalentModifier("OverkillHeal", TalentModifierType.Percent, 5f)
                });
        }

        private static void CreateMeleePath(float baseY)
        {
            // Central path going straight down
            AddMinorNode("w_melee_1", "Melee Prowess I", "+2% Melee Damage", new Vector2(0, baseY - 45),
                new[] { "start_warrior", "w_str_1", "w_vit_1", "w_melee_2" },
                new TalentModifier("MeleeDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("w_melee_2", "Melee Prowess II", "+2% Melee Damage", new Vector2(0, baseY - 80),
                new[] { "w_melee_1", "w_melee_3", "w_aspd_1" },
                new TalentModifier("MeleeDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("w_aspd_1", "Swift Strikes I", "+3% Attack Speed", new Vector2(-25, baseY - 95),
                new[] { "w_melee_2", "w_aspd_2" },
                new TalentModifier("AttackSpeed", TalentModifierType.Percent, 3f), 5);

            AddNotableNode("w_melee_3", "Weapon Expertise", "+5% Melee Damage, +3% Attack Speed", new Vector2(0, baseY - 115),
                new[] { "w_melee_2", "w_melee_4", "w_crit_1" },
                new[] {
                    new TalentModifier("MeleeDamage", TalentModifierType.Percent, 5f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, 3f)
                });

            AddMinorNode("w_aspd_2", "Swift Strikes II", "+3% Attack Speed", new Vector2(-25, baseY - 130),
                new[] { "w_aspd_1", "w_aspd_notable" },
                new TalentModifier("AttackSpeed", TalentModifierType.Percent, 3f), 5);

            AddMinorNode("w_crit_1", "Precision I", "+2% Critical Chance", new Vector2(25, baseY - 130),
                new[] { "w_melee_3", "w_crit_2" },
                new TalentModifier("CritChance", TalentModifierType.Flat, 0.02f), 5);

            AddMinorNode("w_melee_4", "Melee Prowess III", "+2% Melee Damage", new Vector2(0, baseY - 150),
                new[] { "w_melee_3", "w_melee_notable" },
                new TalentModifier("MeleeDamage", TalentModifierType.Percent, 2f), 5);

            AddNotableNode("w_aspd_notable", "Flurry", "+10% Attack Speed, -5% Damage", new Vector2(-40, baseY - 165),
                new[] { "w_aspd_2" },
                new[] {
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, 10f),
                    new TalentModifier("Damage", TalentModifierType.Percent, -5f)
                });

            AddMinorNode("w_crit_2", "Precision II", "+2% Critical Chance", new Vector2(25, baseY - 160),
                new[] { "w_crit_1", "w_crit_dmg_1" },
                new TalentModifier("CritChance", TalentModifierType.Flat, 0.02f), 5);

            AddNotableNode("w_melee_notable", "Master at Arms", "+10% Melee Damage, +5% All Damage", new Vector2(0, baseY - 185),
                new[] { "w_melee_4", "key_warlord" },
                new[] {
                    new TalentModifier("MeleeDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("AllDamage", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("w_crit_dmg_1", "Deadly Precision I", "+10% Critical Damage", new Vector2(45, baseY - 175),
                new[] { "w_crit_2", "w_crit_notable" },
                new TalentModifier("CritDamage", TalentModifierType.Percent, 10f), 5);

            AddNotableNode("w_crit_notable", "Executioner", "+15% Critical Damage, +5% Crit Chance vs low HP", new Vector2(60, baseY - 200),
                new[] { "w_crit_dmg_1" },
                new[] {
                    new TalentModifier("CritDamage", TalentModifierType.Percent, 15f),
                    new TalentModifier("CritChanceVsLowHP", TalentModifierType.Percent, 5f)
                });
        }

        private static void CreateWarriorDefensePath(float baseY)
        {
            // Defensive nodes scattered in warrior area
            AddMinorNode("w_def_resist_1", "Physical Resistance I", "+3% Physical Resistance", new Vector2(-130, baseY - 45),
                new[] { "bridge_wr_1", "w_def_resist_2" },
                new TalentModifier("PhysicalResist", TalentModifierType.Percent, 3f), 5);

            AddMinorNode("w_def_resist_2", "Physical Resistance II", "+3% Physical Resistance", new Vector2(-150, baseY - 70),
                new[] { "w_def_resist_1", "w_def_all_resist" },
                new TalentModifier("PhysicalResist", TalentModifierType.Percent, 3f), 5);

            AddNotableNode("w_def_all_resist", "Hardened", "+5% All Resistances", new Vector2(-170, baseY - 45),
                new[] { "w_def_resist_2" },
                new TalentModifier("AllResist", TalentModifierType.Percent, 5f));

            AddMinorNode("w_def_stam_1", "Endurance I", "+10 Max Stamina", new Vector2(130, baseY - 45),
                new[] { "bridge_ws_1", "w_def_stam_2" },
                new TalentModifier("MaxStamina", TalentModifierType.Flat, 10f), 5);

            AddMinorNode("w_def_stam_2", "Endurance II", "+10 Max Stamina", new Vector2(150, baseY - 70),
                new[] { "w_def_stam_1", "w_def_stam_notable" },
                new TalentModifier("MaxStamina", TalentModifierType.Flat, 10f), 5);

            AddNotableNode("w_def_stam_notable", "Inexhaustible", "+25 Max Stamina, +10% Stamina Regen", new Vector2(170, baseY - 45),
                new[] { "w_def_stam_2" },
                new[] {
                    new TalentModifier("MaxStamina", TalentModifierType.Flat, 25f),
                    new TalentModifier("StaminaRegen", TalentModifierType.Percent, 10f)
                });
        }

        private static void CreateWarriorKeystones(float baseY)
        {
            // Berserker - Low HP damage boost with damage taken penalty
            AddKeystoneNode("key_berserker", "Berserker's Fury",
                "Deal +40% damage when below 30% HP. Take 15% increased damage.",
                new Vector2(-130, baseY - 215),
                new[] { "w_str_notable" },
                new[] {
                    new TalentModifier("LowHealthDamage", TalentModifierType.Percent, 40f),
                    new TalentModifier("DamageTaken", TalentModifierType.Percent, 15f)
                },
                "BerserkerRage"); // Prime ability

            // Juggernaut - Unstoppable tank
            AddKeystoneNode("key_juggernaut", "Juggernaut",
                "Cannot be staggered. +20% Max Health. -25% Movement Speed.",
                new Vector2(130, baseY - 215),
                new[] { "w_vit_notable" },
                new[] {
                    new TalentModifier("StaggerImmune", TalentModifierType.Flat, 1f),
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 20f),
                    new TalentModifier("MoveSpeed", TalentModifierType.Percent, -25f)
                },
                "Juggernaut"); // Prime ability

            // Warlord - Melee mastery
            AddKeystoneNode("key_warlord", "Warlord",
                "Melee attacks have +25% area. +20% Melee Damage. -10% Attack Speed.",
                new Vector2(0, baseY - 230),
                new[] { "w_melee_notable" },
                new[] {
                    new TalentModifier("MeleeArea", TalentModifierType.Percent, 25f),
                    new TalentModifier("MeleeDamage", TalentModifierType.Percent, 20f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, -10f)
                },
                "Warlord"); // Prime ability (passive)

            // Gladiator - Combat sustain
            AddKeystoneNode("key_gladiator", "Gladiator",
                "Kill an enemy to heal 5% HP and gain +15% damage for 5s. Stacks 3x.",
                new Vector2(-65, baseY - 245),
                new[] { "w_str_notable", "w_melee_notable" },
                new[] {
                    new TalentModifier("KillHeal", TalentModifierType.Percent, 5f),
                    new TalentModifier("KillDamageBonus", TalentModifierType.Percent, 15f)
                },
                "GladiatorsGlory"); // Prime ability

            // Iron Skin - Damage reduction
            AddKeystoneNode("key_ironskin", "Iron Skin",
                "Take 20% reduced damage from all sources. -15% Damage dealt.",
                new Vector2(65, baseY - 245),
                new[] { "w_vit_notable", "w_melee_notable" },
                new[] {
                    new TalentModifier("DamageTaken", TalentModifierType.Percent, -20f),
                    new TalentModifier("AllDamage", TalentModifierType.Percent, -15f)
                },
                "IronSkin"); // Prime ability (toggle)
        }

        #endregion

        #region Ranger Branch (West - expands left/outward)

        private static void CreateRangerBranch()
        {
            float baseX = -CENTER_RADIUS;

            // === DEXTERITY PATH ===
            CreateDexterityPath(baseX);

            // === SPEED PATH ===
            CreateSpeedPath(baseX);

            // === RANGED PATH ===
            CreateRangedPath(baseX);

            // === RANGER DEFENSE PATH ===
            CreateRangerDefensePath(baseX);

            // === RANGER KEYSTONES ===
            CreateRangerKeystones(baseX);
        }

        private static void CreateDexterityPath(float baseX)
        {
            AddMinorNode("r_dex_1", "Dexterity I", "+3 Dexterity", new Vector2(baseX - 35, -40),
                new[] { "start_ranger", "r_dex_2", "r_ranged_1" },
                new TalentModifier("Dexterity", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("r_dex_2", "Dexterity II", "+3 Dexterity", new Vector2(baseX - 65, -55),
                new[] { "r_dex_1", "r_dex_3", "r_evasion_1" },
                new TalentModifier("Dexterity", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("r_evasion_1", "Evasion I", "-5% Dodge Stamina Cost", new Vector2(baseX - 55, -80),
                new[] { "r_dex_2", "r_evasion_2" },
                new TalentModifier("DodgeCost", TalentModifierType.Percent, -5f), 5);

            AddNotableNode("r_dex_3", "Agility", "+10 Dexterity, +5% Attack Speed", new Vector2(baseX - 95, -70),
                new[] { "r_dex_2", "r_dex_4", "r_pierce_1" },
                new[] {
                    new TalentModifier("Dexterity", TalentModifierType.Flat, 10f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("r_evasion_2", "Evasion II", "-5% Dodge Stamina Cost", new Vector2(baseX - 75, -105),
                new[] { "r_evasion_1", "r_evasion_notable" },
                new TalentModifier("DodgeCost", TalentModifierType.Percent, -5f), 5);

            AddMinorNode("r_dex_4", "Dexterity III", "+3 Dexterity", new Vector2(baseX - 125, -85),
                new[] { "r_dex_3", "r_dex_notable" },
                new TalentModifier("Dexterity", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("r_pierce_1", "Pierce Mastery I", "+5% Pierce Damage", new Vector2(baseX - 115, -55),
                new[] { "r_dex_3", "r_pierce_2" },
                new TalentModifier("PierceDamage", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("r_evasion_notable", "Wind Dancer", "-15% Dodge Cost, +10% Move Speed after dodge", new Vector2(baseX - 100, -130),
                new[] { "r_evasion_2" },
                new[] {
                    new TalentModifier("DodgeCost", TalentModifierType.Percent, -15f),
                    new TalentModifier("DodgeMoveBonus", TalentModifierType.Percent, 10f)
                });

            AddNotableNode("r_dex_notable", "Lightning Reflexes", "+15 Dexterity, +8% Attack Speed", new Vector2(baseX - 155, -100),
                new[] { "r_dex_4", "key_deadeye" },
                new[] {
                    new TalentModifier("Dexterity", TalentModifierType.Flat, 15f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, 8f)
                });

            AddMinorNode("r_pierce_2", "Pierce Mastery II", "+5% Pierce Damage", new Vector2(baseX - 140, -65),
                new[] { "r_pierce_1", "r_pierce_notable" },
                new TalentModifier("PierceDamage", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("r_pierce_notable", "Armor Piercing", "+10% Pierce Damage, ignore 10% Armor", new Vector2(baseX - 165, -80),
                new[] { "r_pierce_2" },
                new[] {
                    new TalentModifier("PierceDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("ArmorPenetration", TalentModifierType.Flat, 10f)
                });
        }

        private static void CreateSpeedPath(float baseX)
        {
            AddMinorNode("r_speed_1", "Swiftness I", "+3% Movement Speed", new Vector2(baseX - 35, 40),
                new[] { "start_ranger", "r_speed_2", "r_ranged_1" },
                new TalentModifier("MoveSpeed", TalentModifierType.Percent, 3f), 5);

            AddMinorNode("r_speed_2", "Swiftness II", "+3% Movement Speed", new Vector2(baseX - 65, 55),
                new[] { "r_speed_1", "r_speed_3", "r_stam_1" },
                new TalentModifier("MoveSpeed", TalentModifierType.Percent, 3f), 5);

            AddMinorNode("r_stam_1", "Stamina I", "+10 Max Stamina", new Vector2(baseX - 55, 80),
                new[] { "r_speed_2", "r_stam_2" },
                new TalentModifier("MaxStamina", TalentModifierType.Flat, 10f), 5);

            AddNotableNode("r_speed_3", "Fleet Footed", "+8% Movement Speed, +5% Attack Speed", new Vector2(baseX - 95, 70),
                new[] { "r_speed_2", "r_speed_4" },
                new[] {
                    new TalentModifier("MoveSpeed", TalentModifierType.Percent, 8f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("r_stam_2", "Stamina II", "+10 Max Stamina", new Vector2(baseX - 75, 105),
                new[] { "r_stam_1", "r_stam_notable" },
                new TalentModifier("MaxStamina", TalentModifierType.Flat, 10f), 5);

            AddMinorNode("r_speed_4", "Swiftness III", "+3% Movement Speed", new Vector2(baseX - 125, 85),
                new[] { "r_speed_3", "r_speed_notable" },
                new TalentModifier("MoveSpeed", TalentModifierType.Percent, 3f), 5);

            AddNotableNode("r_stam_notable", "Endless Energy", "+30 Max Stamina, +15% Stamina Regen", new Vector2(baseX - 100, 130),
                new[] { "r_stam_2" },
                new[] {
                    new TalentModifier("MaxStamina", TalentModifierType.Flat, 30f),
                    new TalentModifier("StaminaRegen", TalentModifierType.Percent, 15f)
                });

            AddNotableNode("r_speed_notable", "Wind Runner", "+15% Movement Speed, +10% Dodge Distance", new Vector2(baseX - 155, 100),
                new[] { "r_speed_4", "key_windwalker" },
                new[] {
                    new TalentModifier("MoveSpeed", TalentModifierType.Percent, 15f),
                    new TalentModifier("DodgeDistance", TalentModifierType.Percent, 10f)
                });
        }

        private static void CreateRangedPath(float baseX)
        {
            AddMinorNode("r_ranged_1", "Ranged Prowess I", "+2% Ranged Damage", new Vector2(baseX - 45, 0),
                new[] { "start_ranger", "r_dex_1", "r_speed_1", "r_ranged_2" },
                new TalentModifier("RangedDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("r_ranged_2", "Ranged Prowess II", "+2% Ranged Damage", new Vector2(baseX - 80, 0),
                new[] { "r_ranged_1", "r_ranged_3", "r_proj_1" },
                new TalentModifier("RangedDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("r_proj_1", "Projectile Speed I", "+5% Projectile Speed", new Vector2(baseX - 95, -25),
                new[] { "r_ranged_2", "r_proj_2" },
                new TalentModifier("ProjectileSpeed", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("r_ranged_3", "Marksman", "+5% Ranged Damage, +5% Projectile Speed", new Vector2(baseX - 115, 0),
                new[] { "r_ranged_2", "r_ranged_4", "r_bow_1", "r_throw_1" },
                new[] {
                    new TalentModifier("RangedDamage", TalentModifierType.Percent, 5f),
                    new TalentModifier("ProjectileSpeed", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("r_proj_2", "Projectile Speed II", "+5% Projectile Speed", new Vector2(baseX - 130, -25),
                new[] { "r_proj_1", "r_proj_notable" },
                new TalentModifier("ProjectileSpeed", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("r_ranged_4", "Ranged Prowess III", "+2% Ranged Damage", new Vector2(baseX - 150, 0),
                new[] { "r_ranged_3", "r_ranged_notable" },
                new TalentModifier("RangedDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("r_bow_1", "Bow Mastery I", "+5% Bow Damage", new Vector2(baseX - 135, -40),
                new[] { "r_ranged_3", "r_bow_notable" },
                new TalentModifier("BowDamage", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("r_throw_1", "Throwing Mastery", "+5% Throwing Damage", new Vector2(baseX - 135, 40),
                new[] { "r_ranged_3", "r_throw_notable" },
                new TalentModifier("ThrowDamage", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("r_proj_notable", "Velocity", "+15% Projectile Speed, projectiles pierce", new Vector2(baseX - 165, -35),
                new[] { "r_proj_2" },
                new TalentModifier("ProjectileSpeed", TalentModifierType.Percent, 15f));

            AddNotableNode("r_ranged_notable", "Sniper", "+10% Ranged Damage, +10% Crit Damage at range", new Vector2(baseX - 185, 0),
                new[] { "r_ranged_4", "key_sniper" },
                new[] {
                    new TalentModifier("RangedDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("RangeCritDamage", TalentModifierType.Percent, 10f)
                });

            AddNotableNode("r_bow_notable", "Eagle Eye", "+10% Bow Damage, +5% Crit Chance with Bows", new Vector2(baseX - 170, -55),
                new[] { "r_bow_1" },
                new[] {
                    new TalentModifier("BowDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("BowCritChance", TalentModifierType.Flat, 0.05f)
                });

            AddNotableNode("r_throw_notable", "Javelin Master", "+10% Throwing Damage, thrown weapons return", new Vector2(baseX - 170, 55),
                new[] { "r_throw_1" },
                new TalentModifier("ThrowDamage", TalentModifierType.Percent, 10f));
        }

        private static void CreateRangerDefensePath(float baseX)
        {
            AddMinorNode("r_def_dodge_1", "Dodge I", "+5% Dodge Chance", new Vector2(baseX - 45, 110),
                new[] { "bridge_rg_1" },
                new TalentModifier("DodgeChance", TalentModifierType.Percent, 5f), 3);

            AddMinorNode("r_def_dodge_2", "Dodge II", "+5% Dodge Chance", new Vector2(baseX - 70, 130),
                new[] { "r_def_dodge_1", "r_def_dodge_notable" },
                new TalentModifier("DodgeChance", TalentModifierType.Percent, 5f), 3);

            AddNotableNode("r_def_dodge_notable", "Ghost", "+10% Dodge Chance, briefly invisible after dodge", new Vector2(baseX - 95, 150),
                new[] { "r_def_dodge_2" },
                new TalentModifier("DodgeChance", TalentModifierType.Percent, 10f));

            AddMinorNode("r_def_nature_1", "Nature Resist I", "+5% Poison Resistance", new Vector2(baseX - 45, -110),
                new[] { "bridge_rw_1" },
                new TalentModifier("PoisonResist", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("r_def_nature_2", "Nature Resist II", "+5% Poison Resistance", new Vector2(baseX - 70, -130),
                new[] { "r_def_nature_1", "r_def_nature_notable" },
                new TalentModifier("PoisonResist", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("r_def_nature_notable", "Nature's Ward", "+15% Poison Resistance, immune to poison DoT", new Vector2(baseX - 95, -150),
                new[] { "r_def_nature_2" },
                new TalentModifier("PoisonResist", TalentModifierType.Percent, 15f));
        }

        private static void CreateRangerKeystones(float baseX)
        {
            AddKeystoneNode("key_deadeye", "Deadeye",
                "Ranged attacks deal +50% damage but -30% attack speed. Headshots deal double damage.",
                new Vector2(baseX - 190, -120),
                new[] { "r_dex_notable" },
                new[] {
                    new TalentModifier("RangedDamage", TalentModifierType.Percent, 50f),
                    new TalentModifier("AttackSpeed", TalentModifierType.Percent, -30f),
                    new TalentModifier("HeadshotDamage", TalentModifierType.Percent, 100f)
                },
                "Deadeye"); // Prime ability (passive)

            AddKeystoneNode("key_windwalker", "Wind Walker",
                "+50% Movement Speed. Cannot equip heavy armor. +20% Dodge Distance.",
                new Vector2(baseX - 190, 120),
                new[] { "r_speed_notable" },
                new[] {
                    new TalentModifier("MoveSpeed", TalentModifierType.Percent, 50f),
                    new TalentModifier("HeavyArmorDisabled", TalentModifierType.Flat, 1f),
                    new TalentModifier("DodgeDistance", TalentModifierType.Percent, 20f)
                },
                "WindWalker"); // Prime ability (passive)

            AddKeystoneNode("key_sniper", "Sniper",
                "First shot from stealth deals +100% damage. +30% damage at max range.",
                new Vector2(baseX - 220, 0),
                new[] { "r_ranged_notable" },
                new[] {
                    new TalentModifier("StealthDamage", TalentModifierType.Percent, 100f),
                    new TalentModifier("MaxRangeDamage", TalentModifierType.Percent, 30f)
                },
                "Sniper"); // Prime ability (passive)

            AddKeystoneNode("key_assassin", "Shadow Assassin",
                "Attacks from behind deal +40% damage. +25% Crit Chance from behind.",
                new Vector2(baseX - 200, -60),
                new[] { "r_dex_notable", "r_ranged_notable" },
                new[] {
                    new TalentModifier("BackstabDamage", TalentModifierType.Percent, 40f),
                    new TalentModifier("BackstabCrit", TalentModifierType.Flat, 0.25f)
                },
                "ShadowStrike"); // Prime ability (active)

            AddKeystoneNode("key_hunter", "Apex Hunter",
                "Deal +3% damage per 10% of target's missing HP. Tracking arrows.",
                new Vector2(baseX - 200, 60),
                new[] { "r_speed_notable", "r_ranged_notable" },
                new[] {
                    new TalentModifier("ExecuteDamage", TalentModifierType.Percent, 3f)
                },
                "HuntersMark"); // Prime ability (active)
        }

        #endregion

        #region Sorcerer Branch (East - expands right/outward)

        private static void CreateSorcererBranch()
        {
            float baseX = CENTER_RADIUS;

            CreateIntelligencePath(baseX);
            CreateEitrPath(baseX);
            CreateElementalPath(baseX);
            CreateSorcererDefensePath(baseX);
            CreateSorcererKeystones(baseX);
        }

        private static void CreateIntelligencePath(float baseX)
        {
            AddMinorNode("s_int_1", "Intelligence I", "+3 Intelligence", new Vector2(baseX + 35, -40),
                new[] { "start_sorcerer", "s_int_2", "s_elem_1" },
                new TalentModifier("Intelligence", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("s_int_2", "Intelligence II", "+3 Intelligence", new Vector2(baseX + 65, -55),
                new[] { "s_int_1", "s_int_3", "s_spell_1" },
                new TalentModifier("Intelligence", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("s_spell_1", "Spell Power I", "+3% Spell Damage", new Vector2(baseX + 55, -80),
                new[] { "s_int_2", "s_spell_2" },
                new TalentModifier("SpellDamage", TalentModifierType.Percent, 3f), 5);

            AddNotableNode("s_int_3", "Brilliance", "+10 Intelligence, +5% Spell Damage", new Vector2(baseX + 95, -70),
                new[] { "s_int_2", "s_int_4", "s_cdr_1" },
                new[] {
                    new TalentModifier("Intelligence", TalentModifierType.Flat, 10f),
                    new TalentModifier("SpellDamage", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("s_spell_2", "Spell Power II", "+3% Spell Damage", new Vector2(baseX + 75, -105),
                new[] { "s_spell_1", "s_spell_notable" },
                new TalentModifier("SpellDamage", TalentModifierType.Percent, 3f), 5);

            AddMinorNode("s_int_4", "Intelligence III", "+3 Intelligence", new Vector2(baseX + 125, -85),
                new[] { "s_int_3", "s_int_notable" },
                new TalentModifier("Intelligence", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("s_cdr_1", "Cooldown I", "+3% Cooldown Reduction", new Vector2(baseX + 115, -55),
                new[] { "s_int_3", "s_cdr_2" },
                new TalentModifier("CooldownReduction", TalentModifierType.Flat, 0.03f), 5);

            AddNotableNode("s_spell_notable", "Arcane Focus", "+10% Spell Damage, +5% Spell Crit", new Vector2(baseX + 100, -130),
                new[] { "s_spell_2" },
                new[] {
                    new TalentModifier("SpellDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("SpellCrit", TalentModifierType.Flat, 0.05f)
                });

            AddNotableNode("s_int_notable", "Sage's Wisdom", "+15 Intelligence, +8% Max Eitr", new Vector2(baseX + 155, -100),
                new[] { "s_int_4", "key_archmage" },
                new[] {
                    new TalentModifier("Intelligence", TalentModifierType.Flat, 15f),
                    new TalentModifier("MaxEitr", TalentModifierType.Percent, 8f)
                });

            AddMinorNode("s_cdr_2", "Cooldown II", "+3% Cooldown Reduction", new Vector2(baseX + 140, -65),
                new[] { "s_cdr_1", "s_cdr_notable" },
                new TalentModifier("CooldownReduction", TalentModifierType.Flat, 0.03f), 5);

            AddNotableNode("s_cdr_notable", "Time Warp", "+10% Cooldown Reduction, spells cost -10% Eitr", new Vector2(baseX + 165, -80),
                new[] { "s_cdr_2" },
                new[] {
                    new TalentModifier("CooldownReduction", TalentModifierType.Flat, 0.10f),
                    new TalentModifier("SpellCost", TalentModifierType.Percent, -10f)
                });
        }

        private static void CreateEitrPath(float baseX)
        {
            AddMinorNode("s_eitr_1", "Eitr Pool I", "+10 Max Eitr", new Vector2(baseX + 35, 40),
                new[] { "start_sorcerer", "s_eitr_2", "s_elem_1" },
                new TalentModifier("MaxEitr", TalentModifierType.Flat, 10f), 5);

            AddMinorNode("s_eitr_2", "Eitr Pool II", "+10 Max Eitr", new Vector2(baseX + 65, 55),
                new[] { "s_eitr_1", "s_eitr_3", "s_eitr_regen_1" },
                new TalentModifier("MaxEitr", TalentModifierType.Flat, 10f), 5);

            AddMinorNode("s_eitr_regen_1", "Eitr Regen I", "+5% Eitr Regen", new Vector2(baseX + 55, 80),
                new[] { "s_eitr_2", "s_eitr_regen_2" },
                new TalentModifier("EitrRegen", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("s_eitr_3", "Mana Well", "+20 Max Eitr, +5% Eitr Regen", new Vector2(baseX + 95, 70),
                new[] { "s_eitr_2", "s_eitr_4" },
                new[] {
                    new TalentModifier("MaxEitr", TalentModifierType.Flat, 20f),
                    new TalentModifier("EitrRegen", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("s_eitr_regen_2", "Eitr Regen II", "+5% Eitr Regen", new Vector2(baseX + 75, 105),
                new[] { "s_eitr_regen_1", "s_eitr_regen_notable" },
                new TalentModifier("EitrRegen", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("s_eitr_4", "Eitr Pool III", "+10 Max Eitr", new Vector2(baseX + 125, 85),
                new[] { "s_eitr_3", "s_eitr_notable" },
                new TalentModifier("MaxEitr", TalentModifierType.Flat, 10f), 5);

            AddNotableNode("s_eitr_regen_notable", "Infinite Flow", "+15% Eitr Regen, restore 5% on kill", new Vector2(baseX + 100, 130),
                new[] { "s_eitr_regen_2" },
                new[] {
                    new TalentModifier("EitrRegen", TalentModifierType.Percent, 15f),
                    new TalentModifier("EitrOnKill", TalentModifierType.Percent, 5f)
                });

            AddNotableNode("s_eitr_notable", "Mana Overflow", "+40 Max Eitr, excess Eitr becomes spell damage", new Vector2(baseX + 155, 100),
                new[] { "s_eitr_4", "key_battlemage" },
                new[] {
                    new TalentModifier("MaxEitr", TalentModifierType.Flat, 40f),
                    new TalentModifier("EitrOverflow", TalentModifierType.Flat, 1f)
                });
        }

        private static void CreateElementalPath(float baseX)
        {
            AddMinorNode("s_elem_1", "Elemental Mastery I", "+2% Elemental Damage", new Vector2(baseX + 45, 0),
                new[] { "start_sorcerer", "s_int_1", "s_eitr_1", "s_elem_2" },
                new TalentModifier("ElementalDamage", TalentModifierType.Percent, 2f), 5);

            AddMinorNode("s_elem_2", "Elemental Mastery II", "+2% Elemental Damage", new Vector2(baseX + 80, 0),
                new[] { "s_elem_1", "s_elem_3", "s_fire_1", "s_frost_1", "s_lightning_1" },
                new TalentModifier("ElementalDamage", TalentModifierType.Percent, 2f), 5);

            AddNotableNode("s_elem_3", "Elemental Affinity", "+5% Elemental Damage, +5% Elemental Resist", new Vector2(baseX + 115, 0),
                new[] { "s_elem_2", "s_elem_notable" },
                new[] {
                    new TalentModifier("ElementalDamage", TalentModifierType.Percent, 5f),
                    new TalentModifier("AllResist", TalentModifierType.Percent, 5f)
                });

            // Fire branch
            AddMinorNode("s_fire_1", "Fire Mastery I", "+4% Fire Damage", new Vector2(baseX + 95, -30),
                new[] { "s_elem_2", "s_fire_2" },
                new TalentModifier("FireDamage", TalentModifierType.Percent, 4f), 5);

            AddMinorNode("s_fire_2", "Fire Mastery II", "+4% Fire Damage", new Vector2(baseX + 120, -50),
                new[] { "s_fire_1", "s_fire_notable" },
                new TalentModifier("FireDamage", TalentModifierType.Percent, 4f), 5);

            AddNotableNode("s_fire_notable", "Inferno", "+12% Fire Damage, fire spells ignite enemies", new Vector2(baseX + 145, -70),
                new[] { "s_fire_2", "key_pyromancer" },
                new[] {
                    new TalentModifier("FireDamage", TalentModifierType.Percent, 12f),
                    new TalentModifier("FireIgnite", TalentModifierType.Flat, 1f)
                });

            // Frost branch
            AddMinorNode("s_frost_1", "Frost Mastery I", "+4% Frost Damage", new Vector2(baseX + 95, 30),
                new[] { "s_elem_2", "s_frost_2" },
                new TalentModifier("FrostDamage", TalentModifierType.Percent, 4f), 5);

            AddMinorNode("s_frost_2", "Frost Mastery II", "+4% Frost Damage", new Vector2(baseX + 120, 50),
                new[] { "s_frost_1", "s_frost_notable" },
                new TalentModifier("FrostDamage", TalentModifierType.Percent, 4f), 5);

            AddNotableNode("s_frost_notable", "Blizzard", "+12% Frost Damage, frost spells slow enemies", new Vector2(baseX + 145, 70),
                new[] { "s_frost_2", "key_cryomancer" },
                new[] {
                    new TalentModifier("FrostDamage", TalentModifierType.Percent, 12f),
                    new TalentModifier("FrostSlow", TalentModifierType.Flat, 1f)
                });

            // Lightning branch
            AddMinorNode("s_lightning_1", "Lightning Mastery I", "+4% Lightning Damage", new Vector2(baseX + 80, -45),
                new[] { "s_elem_2", "s_lightning_2" },
                new TalentModifier("LightningDamage", TalentModifierType.Percent, 4f), 5);

            AddMinorNode("s_lightning_2", "Lightning Mastery II", "+4% Lightning Damage", new Vector2(baseX + 100, -65),
                new[] { "s_lightning_1", "s_lightning_notable" },
                new TalentModifier("LightningDamage", TalentModifierType.Percent, 4f), 5);

            AddNotableNode("s_lightning_notable", "Tempest", "+12% Lightning Damage, lightning chains", new Vector2(baseX + 125, -85),
                new[] { "s_lightning_2" },
                new[] {
                    new TalentModifier("LightningDamage", TalentModifierType.Percent, 12f),
                    new TalentModifier("LightningChain", TalentModifierType.Flat, 1f)
                });

            AddNotableNode("s_elem_notable", "Elemental Mastery", "+10% All Elemental Damage", new Vector2(baseX + 150, 0),
                new[] { "s_elem_3", "key_elementalist" },
                new TalentModifier("ElementalDamage", TalentModifierType.Percent, 10f));
        }

        private static void CreateSorcererDefensePath(float baseX)
        {
            AddMinorNode("s_def_ward_1", "Magic Ward I", "+5% Magic Resistance", new Vector2(baseX + 45, 110),
                new[] { "bridge_sg_1" },
                new TalentModifier("MagicResist", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("s_def_ward_2", "Magic Ward II", "+5% Magic Resistance", new Vector2(baseX + 70, 130),
                new[] { "s_def_ward_1", "s_def_ward_notable" },
                new TalentModifier("MagicResist", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("s_def_ward_notable", "Spell Shield", "+15% Magic Resistance, chance to reflect spells", new Vector2(baseX + 95, 150),
                new[] { "s_def_ward_2" },
                new[] {
                    new TalentModifier("MagicResist", TalentModifierType.Percent, 15f),
                    new TalentModifier("SpellReflect", TalentModifierType.Percent, 10f)
                });

            AddMinorNode("s_def_barrier_1", "Barrier I", "+5% Shield Strength", new Vector2(baseX + 45, -110),
                new[] { "bridge_sw_1" },
                new TalentModifier("ShieldStrength", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("s_def_barrier_2", "Barrier II", "+5% Shield Strength", new Vector2(baseX + 70, -130),
                new[] { "s_def_barrier_1", "s_def_barrier_notable" },
                new TalentModifier("ShieldStrength", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("s_def_barrier_notable", "Arcane Barrier", "+15% Shield Strength, shields explode when broken", new Vector2(baseX + 95, -150),
                new[] { "s_def_barrier_2" },
                new TalentModifier("ShieldStrength", TalentModifierType.Percent, 15f));
        }

        private static void CreateSorcererKeystones(float baseX)
        {
            AddKeystoneNode("key_archmage", "Archmage",
                "Spells deal +40% damage and cost +30% more Eitr. +20 Intelligence.",
                new Vector2(baseX + 190, -120),
                new[] { "s_int_notable" },
                new[] {
                    new TalentModifier("SpellDamage", TalentModifierType.Percent, 40f),
                    new TalentModifier("SpellCost", TalentModifierType.Percent, 30f),
                    new TalentModifier("Intelligence", TalentModifierType.Flat, 20f)
                },
                "Archmage"); // Prime ability (passive)

            AddKeystoneNode("key_battlemage", "Battle Mage",
                "Melee attacks restore 5% Eitr. +15% Melee and Spell Damage.",
                new Vector2(baseX + 190, 120),
                new[] { "s_eitr_notable" },
                new[] {
                    new TalentModifier("MeleeEitrRestore", TalentModifierType.Percent, 5f),
                    new TalentModifier("MeleeDamage", TalentModifierType.Percent, 15f),
                    new TalentModifier("SpellDamage", TalentModifierType.Percent, 15f)
                },
                "BattleMage"); // Prime ability (passive)

            AddKeystoneNode("key_elementalist", "Elementalist",
                "Casting a spell of one element buffs the next different element by 30%.",
                new Vector2(baseX + 190, 0),
                new[] { "s_elem_notable" },
                new TalentModifier("ElementalRotation", TalentModifierType.Flat, 30f),
                "ElementalRotation"); // Prime ability (passive)

            AddKeystoneNode("key_pyromancer", "Pyromancer",
                "+50% Fire Damage. Enemies killed by fire explode. Take 15% more Frost damage.",
                new Vector2(baseX + 180, -95),
                new[] { "s_fire_notable" },
                new[] {
                    new TalentModifier("FireDamage", TalentModifierType.Percent, 50f),
                    new TalentModifier("FireExplosion", TalentModifierType.Flat, 1f),
                    new TalentModifier("FrostResist", TalentModifierType.Percent, -15f)
                },
                "Pyromancer"); // Prime ability (passive)

            AddKeystoneNode("key_cryomancer", "Cryomancer",
                "+50% Frost Damage. Frozen enemies shatter for AoE damage. Take 15% more Fire damage.",
                new Vector2(baseX + 180, 95),
                new[] { "s_frost_notable" },
                new[] {
                    new TalentModifier("FrostDamage", TalentModifierType.Percent, 50f),
                    new TalentModifier("FrostShatter", TalentModifierType.Flat, 1f),
                    new TalentModifier("FireResist", TalentModifierType.Percent, -15f)
                },
                "Cryomancer"); // Prime ability (passive)
        }

        #endregion

        #region Guardian Branch (North - expands up/outward)

        private static void CreateGuardianBranch()
        {
            float baseY = CENTER_RADIUS;

            CreateSpiritPath(baseY);
            CreateHealingPath(baseY);
            CreateWardPath(baseY);
            CreateGuardianDefensePath(baseY);
            CreateGuardianKeystones(baseY);
        }

        private static void CreateSpiritPath(float baseY)
        {
            AddMinorNode("g_spirit_1", "Spirit I", "+3 Spirit", new Vector2(-40, baseY + 35),
                new[] { "start_guardian", "g_spirit_2", "g_healing_1" },
                new TalentModifier("SpiritDamage", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("g_spirit_2", "Spirit II", "+3 Spirit", new Vector2(-55, baseY + 65),
                new[] { "g_spirit_1", "g_spirit_3", "g_holy_1" },
                new TalentModifier("SpiritDamage", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("g_holy_1", "Holy Power I", "+3% Spirit Damage", new Vector2(-80, baseY + 55),
                new[] { "g_spirit_2", "g_holy_2" },
                new TalentModifier("SpiritDamage", TalentModifierType.Percent, 3f), 5);

            AddNotableNode("g_spirit_3", "Divine Presence", "+10 Spirit, +5% Healing Power", new Vector2(-70, baseY + 95),
                new[] { "g_spirit_2", "g_spirit_4", "g_aura_1" },
                new[] {
                    new TalentModifier("SpiritDamage", TalentModifierType.Flat, 10f),
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("g_holy_2", "Holy Power II", "+3% Spirit Damage", new Vector2(-105, baseY + 75),
                new[] { "g_holy_1", "g_holy_notable" },
                new TalentModifier("SpiritDamage", TalentModifierType.Percent, 3f), 5);

            AddMinorNode("g_spirit_4", "Spirit III", "+3 Spirit", new Vector2(-85, baseY + 125),
                new[] { "g_spirit_3", "g_spirit_notable" },
                new TalentModifier("SpiritDamage", TalentModifierType.Flat, 3f), 5);

            AddMinorNode("g_aura_1", "Aura Range I", "+10% Aura Radius", new Vector2(-45, baseY + 110),
                new[] { "g_spirit_3", "g_aura_notable" },
                new TalentModifier("AuraRadius", TalentModifierType.Percent, 10f), 3);

            AddNotableNode("g_holy_notable", "Radiance", "+10% Spirit Damage, spirit attacks heal allies", new Vector2(-130, baseY + 95),
                new[] { "g_holy_2" },
                new[] {
                    new TalentModifier("SpiritDamage", TalentModifierType.Percent, 10f),
                    new TalentModifier("SpiritHeal", TalentModifierType.Flat, 1f)
                });

            AddNotableNode("g_spirit_notable", "Avatar of Light", "+15 Spirit, +10% Max Health", new Vector2(-100, baseY + 155),
                new[] { "g_spirit_4", "key_paladin" },
                new[] {
                    new TalentModifier("SpiritDamage", TalentModifierType.Flat, 15f),
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 10f)
                });

            AddNotableNode("g_aura_notable", "Beacon", "+25% Aura Radius, auras persist 3s after leaving range", new Vector2(-30, baseY + 140),
                new[] { "g_aura_1" },
                new TalentModifier("AuraRadius", TalentModifierType.Percent, 25f));
        }

        private static void CreateHealingPath(float baseY)
        {
            AddMinorNode("g_healing_1", "Healing Power I", "+5% Healing Effectiveness", new Vector2(0, baseY + 45),
                new[] { "start_guardian", "g_spirit_1", "g_ward_1", "g_healing_2" },
                new TalentModifier("HealingPower", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("g_healing_2", "Healing Power II", "+5% Healing Effectiveness", new Vector2(0, baseY + 80),
                new[] { "g_healing_1", "g_healing_3", "g_regen_heal_1" },
                new TalentModifier("HealingPower", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("g_regen_heal_1", "Health Regen I", "+5% Health Regen", new Vector2(-25, baseY + 95),
                new[] { "g_healing_2", "g_regen_heal_2" },
                new TalentModifier("HealthRegen", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("g_healing_3", "Blessed Touch", "+10% Healing, heals remove 1 debuff", new Vector2(0, baseY + 115),
                new[] { "g_healing_2", "g_healing_4" },
                new[] {
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 10f),
                    new TalentModifier("HealDispel", TalentModifierType.Flat, 1f)
                });

            AddMinorNode("g_regen_heal_2", "Health Regen II", "+5% Health Regen", new Vector2(-25, baseY + 130),
                new[] { "g_regen_heal_1", "g_regen_notable" },
                new TalentModifier("HealthRegen", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("g_healing_4", "Healing Power III", "+5% Healing", new Vector2(0, baseY + 150),
                new[] { "g_healing_3", "g_healing_notable" },
                new TalentModifier("HealingPower", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("g_regen_notable", "Regeneration Aura", "+15% Health Regen, nearby allies gain 50%", new Vector2(-40, baseY + 165),
                new[] { "g_regen_heal_2" },
                new[] {
                    new TalentModifier("HealthRegen", TalentModifierType.Percent, 15f),
                    new TalentModifier("AllyRegen", TalentModifierType.Percent, 50f)
                });

            AddNotableNode("g_healing_notable", "Divine Healer", "+15% Healing, critical heals restore stamina", new Vector2(0, baseY + 185),
                new[] { "g_healing_4", "key_high_priest" },
                new[] {
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 15f),
                    new TalentModifier("CritHealStamina", TalentModifierType.Flat, 1f)
                });
        }

        private static void CreateWardPath(float baseY)
        {
            AddMinorNode("g_ward_1", "Warding I", "+5 Armor, +3% All Resist", new Vector2(40, baseY + 35),
                new[] { "start_guardian", "g_ward_2", "g_healing_1" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 5f),
                    new TalentModifier("AllResist", TalentModifierType.Percent, 3f)
                }, 5);

            AddMinorNode("g_ward_2", "Warding II", "+5 Armor, +3% All Resist", new Vector2(55, baseY + 65),
                new[] { "g_ward_1", "g_ward_3", "g_block_g_1" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 5f),
                    new TalentModifier("AllResist", TalentModifierType.Percent, 3f)
                }, 5);

            AddMinorNode("g_block_g_1", "Shield Mastery I", "+8 Block Power", new Vector2(80, baseY + 55),
                new[] { "g_ward_2", "g_block_g_2" },
                new TalentModifier("BlockPower", TalentModifierType.Flat, 8f), 5);

            AddNotableNode("g_ward_3", "Sanctuary", "+10 Armor, +5% All Resist", new Vector2(70, baseY + 95),
                new[] { "g_ward_2", "g_ward_4", "g_thorns_1" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 10f),
                    new TalentModifier("AllResist", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("g_block_g_2", "Shield Mastery II", "+8 Block Power", new Vector2(105, baseY + 75),
                new[] { "g_block_g_1", "g_block_notable" },
                new TalentModifier("BlockPower", TalentModifierType.Flat, 8f), 5);

            AddMinorNode("g_ward_4", "Warding III", "+5 Armor, +3% All Resist", new Vector2(85, baseY + 125),
                new[] { "g_ward_3", "g_ward_notable" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 5f),
                    new TalentModifier("AllResist", TalentModifierType.Percent, 3f)
                }, 5);

            AddMinorNode("g_thorns_1", "Thorns I", "Reflect 5% damage taken", new Vector2(45, baseY + 110),
                new[] { "g_ward_3", "g_thorns_notable" },
                new TalentModifier("ThornsReflect", TalentModifierType.Percent, 5f), 3);

            AddNotableNode("g_block_notable", "Bulwark", "+20 Block Power, parries stagger nearby enemies", new Vector2(130, baseY + 95),
                new[] { "g_block_g_2" },
                new[] {
                    new TalentModifier("BlockPower", TalentModifierType.Flat, 20f),
                    new TalentModifier("ParryStagger", TalentModifierType.Flat, 1f)
                });

            AddNotableNode("g_ward_notable", "Fortress", "+20 Armor, +10% All Resist", new Vector2(100, baseY + 155),
                new[] { "g_ward_4", "key_bastion" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 20f),
                    new TalentModifier("AllResist", TalentModifierType.Percent, 10f)
                });

            AddNotableNode("g_thorns_notable", "Retribution", "Reflect 15% damage, thorns crit for double", new Vector2(30, baseY + 140),
                new[] { "g_thorns_1" },
                new TalentModifier("ThornsReflect", TalentModifierType.Percent, 15f));
        }

        private static void CreateGuardianDefensePath(float baseY)
        {
            AddMinorNode("g_def_prot_1", "Protection I", "+5% Damage Reduction", new Vector2(-130, baseY + 45),
                new[] { "bridge_gr_1" },
                new TalentModifier("DamageReduction", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("g_def_prot_2", "Protection II", "+5% Damage Reduction", new Vector2(-150, baseY + 70),
                new[] { "g_def_prot_1", "g_def_prot_notable" },
                new TalentModifier("DamageReduction", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("g_def_prot_notable", "Guardian Angel", "+10% Damage Reduction, survive lethal hit once", new Vector2(-170, baseY + 45),
                new[] { "g_def_prot_2" },
                new[] {
                    new TalentModifier("DamageReduction", TalentModifierType.Percent, 10f),
                    new TalentModifier("CheatDeath", TalentModifierType.Flat, 1f)
                });

            AddMinorNode("g_def_ally_1", "Ally Shield I", "+5% Team Damage Reduction", new Vector2(130, baseY + 45),
                new[] { "bridge_gs_1" },
                new TalentModifier("TeamDamageReduction", TalentModifierType.Percent, 5f), 5);

            AddMinorNode("g_def_ally_2", "Ally Shield II", "+5% Team Damage Reduction", new Vector2(150, baseY + 70),
                new[] { "g_def_ally_1", "g_def_ally_notable" },
                new TalentModifier("TeamDamageReduction", TalentModifierType.Percent, 5f), 5);

            AddNotableNode("g_def_ally_notable", "Martyr", "+10% Team DR, take 50% of ally damage", new Vector2(170, baseY + 45),
                new[] { "g_def_ally_2" },
                new[] {
                    new TalentModifier("TeamDamageReduction", TalentModifierType.Percent, 10f),
                    new TalentModifier("AllyDamageShare", TalentModifierType.Percent, 50f)
                });
        }

        private static void CreateGuardianKeystones(float baseY)
        {
            AddKeystoneNode("key_paladin", "Paladin",
                "Deal +30% Spirit damage. Heal 3% HP on hit. Take -20% Physical Damage.",
                new Vector2(-115, baseY + 185),
                new[] { "g_spirit_notable" },
                new[] {
                    new TalentModifier("SpiritDamage", TalentModifierType.Percent, 30f),
                    new TalentModifier("HitHeal", TalentModifierType.Percent, 3f),
                    new TalentModifier("PhysicalResist", TalentModifierType.Percent, 20f)
                },
                "Paladin"); // Prime ability (passive)

            AddKeystoneNode("key_high_priest", "High Priest",
                "Healing spells are 50% more effective. Cannot deal damage directly.",
                new Vector2(0, baseY + 220),
                new[] { "g_healing_notable" },
                new[] {
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 50f),
                    new TalentModifier("CannotDealDamage", TalentModifierType.Flat, 1f)
                },
                "MassHeal"); // Prime ability (active)

            AddKeystoneNode("key_bastion", "Bastion",
                "+50% Armor and Block Power. -30% Movement Speed. Allies near you take -15% damage.",
                new Vector2(115, baseY + 185),
                new[] { "g_ward_notable" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Percent, 50f),
                    new TalentModifier("BlockPower", TalentModifierType.Percent, 50f),
                    new TalentModifier("MoveSpeed", TalentModifierType.Percent, -30f),
                    new TalentModifier("AllyDamageReduction", TalentModifierType.Percent, 15f)
                },
                "Bastion"); // Prime ability (passive)

            AddKeystoneNode("key_templar", "Templar",
                "Block all damage for 2s after taking a hit (30s cooldown). +20% Max Health.",
                new Vector2(-60, baseY + 210),
                new[] { "g_spirit_notable", "g_healing_notable" },
                new[] {
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 20f)
                },
                "DivineShield"); // Prime ability (active)

            AddKeystoneNode("key_warden", "Warden",
                "Taunt enemies in range. +30% Armor. Take +20% damage.",
                new Vector2(60, baseY + 210),
                new[] { "g_healing_notable", "g_ward_notable" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Percent, 30f),
                    new TalentModifier("DamageTaken", TalentModifierType.Percent, 20f)
                },
                "Taunt"); // Prime ability (active)
        }

        #endregion

        #region Bridge Nodes (Connections between class areas)

        private static void CreateBridgeNodes()
        {
            // Warrior-Ranger bridge (Southwest)
            AddMinorNode("bridge_wr_1", "Hybrid Training I", "+2% All Damage", new Vector2(-90, -60),
                new[] { "w_def_resist_1", "r_def_nature_1", "bridge_wr_2" },
                new TalentModifier("AllDamage", TalentModifierType.Percent, 2f), 3);

            AddNotableNode("bridge_wr_2", "Battle Ranger", "+5% Melee and Ranged Damage", new Vector2(-110, -40),
                new[] { "bridge_wr_1" },
                new[] {
                    new TalentModifier("MeleeDamage", TalentModifierType.Percent, 5f),
                    new TalentModifier("RangedDamage", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("bridge_rw_1", "Combat Instincts", "+5% Attack Speed", new Vector2(-60, -90),
                new[] { "bridge_wr_1", "r_def_nature_1" },
                new TalentModifier("AttackSpeed", TalentModifierType.Percent, 5f), 3);

            // Warrior-Sorcerer bridge (Southeast)
            AddMinorNode("bridge_ws_1", "Spell Blade I", "+2% Physical and Elemental Damage", new Vector2(90, -60),
                new[] { "w_def_stam_1", "s_def_barrier_1", "bridge_ws_2" },
                new[] {
                    new TalentModifier("PhysicalDamage", TalentModifierType.Percent, 2f),
                    new TalentModifier("ElementalDamage", TalentModifierType.Percent, 2f)
                }, 3);

            AddNotableNode("bridge_ws_2", "Arcane Warrior", "+5% Melee and Spell Damage", new Vector2(110, -40),
                new[] { "bridge_ws_1" },
                new[] {
                    new TalentModifier("MeleeDamage", TalentModifierType.Percent, 5f),
                    new TalentModifier("SpellDamage", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("bridge_sw_1", "Elemental Strikes", "+5% Weapon Elemental Damage", new Vector2(60, -90),
                new[] { "bridge_ws_1", "s_def_barrier_1" },
                new TalentModifier("WeaponElementalDamage", TalentModifierType.Percent, 5f), 3);

            // Ranger-Guardian bridge (Northwest)
            AddMinorNode("bridge_rg_1", "Nature's Blessing I", "+5% Health and Stamina Regen", new Vector2(-90, 60),
                new[] { "r_def_dodge_1", "g_def_prot_1", "bridge_rg_2" },
                new[] {
                    new TalentModifier("HealthRegen", TalentModifierType.Percent, 5f),
                    new TalentModifier("StaminaRegen", TalentModifierType.Percent, 5f)
                }, 3);

            AddNotableNode("bridge_rg_2", "Druid's Path", "+8% Nature Resist, +5% Healing Received", new Vector2(-110, 40),
                new[] { "bridge_rg_1" },
                new[] {
                    new TalentModifier("PoisonResist", TalentModifierType.Percent, 8f),
                    new TalentModifier("HealingReceived", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("bridge_gr_1", "Swift Recovery", "+10% Dodge, +5% Healing", new Vector2(-60, 90),
                new[] { "bridge_rg_1", "g_def_prot_1" },
                new[] {
                    new TalentModifier("DodgeChance", TalentModifierType.Percent, 10f),
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 5f)
                }, 3);

            // Sorcerer-Guardian bridge (Northeast)
            AddMinorNode("bridge_sg_1", "Holy Magic I", "+3% Spirit and Elemental Damage", new Vector2(90, 60),
                new[] { "s_def_ward_1", "g_def_ally_1", "bridge_sg_2" },
                new[] {
                    new TalentModifier("SpiritDamage", TalentModifierType.Percent, 3f),
                    new TalentModifier("ElementalDamage", TalentModifierType.Percent, 3f)
                }, 3);

            AddNotableNode("bridge_sg_2", "Divine Caster", "+8% Healing, +5% Spell Damage", new Vector2(110, 40),
                new[] { "bridge_sg_1" },
                new[] {
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 8f),
                    new TalentModifier("SpellDamage", TalentModifierType.Percent, 5f)
                });

            AddMinorNode("bridge_gs_1", "Sacred Power", "+5% Max Eitr, +5% Healing", new Vector2(60, 90),
                new[] { "bridge_sg_1", "g_def_ally_1" },
                new[] {
                    new TalentModifier("MaxEitr", TalentModifierType.Percent, 5f),
                    new TalentModifier("HealingPower", TalentModifierType.Percent, 5f)
                }, 3);
        }

        #endregion

        #region Defensive Ring (Central nodes accessible from all starts)

        private static void CreateDefensiveRing()
        {
            float ringRadius = CENTER_RADIUS * 0.5f;

            // South (from Warrior)
            AddMinorNode("def_ring_s", "Survival I", "+5 Armor, +10 Health", new Vector2(0, -ringRadius),
                new[] { "start_warrior", "def_ring_sw", "def_ring_se", "def_center" },
                new[] {
                    new TalentModifier("Armor", TalentModifierType.Flat, 5f),
                    new TalentModifier("MaxHealth", TalentModifierType.Flat, 10f)
                }, 3);

            // West (from Ranger)
            AddMinorNode("def_ring_w", "Agility I", "+5% Dodge, +5% Move Speed", new Vector2(-ringRadius, 0),
                new[] { "start_ranger", "def_ring_sw", "def_ring_nw", "def_center" },
                new[] {
                    new TalentModifier("DodgeChance", TalentModifierType.Percent, 5f),
                    new TalentModifier("MoveSpeed", TalentModifierType.Percent, 5f)
                }, 3);

            // East (from Sorcerer)
            AddMinorNode("def_ring_e", "Resilience I", "+5% All Resist", new Vector2(ringRadius, 0),
                new[] { "start_sorcerer", "def_ring_se", "def_ring_ne", "def_center" },
                new TalentModifier("AllResist", TalentModifierType.Percent, 5f), 3);

            // North (from Guardian)
            AddMinorNode("def_ring_n", "Fortification I", "+10 Armor", new Vector2(0, ringRadius),
                new[] { "start_guardian", "def_ring_nw", "def_ring_ne", "def_center" },
                new TalentModifier("Armor", TalentModifierType.Flat, 10f), 3);

            // Corners
            AddMinorNode("def_ring_sw", "Vitality", "+15 Health, +10 Stamina", new Vector2(-ringRadius * 0.7f, -ringRadius * 0.7f),
                new[] { "def_ring_s", "def_ring_w" },
                new[] {
                    new TalentModifier("MaxHealth", TalentModifierType.Flat, 15f),
                    new TalentModifier("MaxStamina", TalentModifierType.Flat, 10f)
                }, 3);

            AddMinorNode("def_ring_se", "Endurance", "+5% Health Regen, +5% Stamina Regen", new Vector2(ringRadius * 0.7f, -ringRadius * 0.7f),
                new[] { "def_ring_s", "def_ring_e" },
                new[] {
                    new TalentModifier("HealthRegen", TalentModifierType.Percent, 5f),
                    new TalentModifier("StaminaRegen", TalentModifierType.Percent, 5f)
                }, 3);

            AddMinorNode("def_ring_nw", "Toughness", "+3% Damage Reduction", new Vector2(-ringRadius * 0.7f, ringRadius * 0.7f),
                new[] { "def_ring_w", "def_ring_n" },
                new TalentModifier("DamageReduction", TalentModifierType.Percent, 3f), 3);

            AddMinorNode("def_ring_ne", "Warding", "+8% Magic Resist", new Vector2(ringRadius * 0.7f, ringRadius * 0.7f),
                new[] { "def_ring_e", "def_ring_n" },
                new TalentModifier("MagicResist", TalentModifierType.Percent, 8f), 3);

            // Center node
            AddNotableNode("def_center", "Survivor's Instinct", "+3% All Resist, +5% Max Health, +5% Dodge",
                new Vector2(0, 0),
                new[] { "def_ring_s", "def_ring_w", "def_ring_e", "def_ring_n" },
                new[] {
                    new TalentModifier("AllResist", TalentModifierType.Percent, 3f),
                    new TalentModifier("MaxHealth", TalentModifierType.Percent, 5f),
                    new TalentModifier("DodgeChance", TalentModifierType.Percent, 5f)
                });
        }

        #endregion

        #region Helper Methods

        private static void AddNode(TalentNode node)
        {
            _nodes[node.Id] = node;
        }

        private static void AddMinorNode(string id, string name, string desc, Vector2 pos,
            string[] connections, TalentModifier mod, int maxRanks = 1)
        {
            AddNode(new TalentNode
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = TalentNodeType.Minor,
                Position = pos,
                MaxRanks = maxRanks,
                Connections = connections.ToList(),
                Modifiers = new List<TalentModifier> { mod }
            });
        }

        private static void AddMinorNode(string id, string name, string desc, Vector2 pos,
            string[] connections, TalentModifier[] mods, int maxRanks = 1)
        {
            AddNode(new TalentNode
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = TalentNodeType.Minor,
                Position = pos,
                MaxRanks = maxRanks,
                Connections = connections.ToList(),
                Modifiers = mods.ToList()
            });
        }

        private static void AddNotableNode(string id, string name, string desc, Vector2 pos,
            string[] connections, TalentModifier mod)
        {
            AddNode(new TalentNode
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = TalentNodeType.Notable,
                Position = pos,
                Connections = connections.ToList(),
                Modifiers = new List<TalentModifier> { mod }
            });
        }

        private static void AddNotableNode(string id, string name, string desc, Vector2 pos,
            string[] connections, TalentModifier[] mods)
        {
            AddNode(new TalentNode
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = TalentNodeType.Notable,
                Position = pos,
                Connections = connections.ToList(),
                Modifiers = mods.ToList()
            });
        }

        private static void AddKeystoneNode(string id, string name, string desc, Vector2 pos,
            string[] connections, TalentModifier mod, string grantsAbility = null)
        {
            AddNode(new TalentNode
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = TalentNodeType.Keystone,
                Position = pos,
                Connections = connections.ToList(),
                Modifiers = new List<TalentModifier> { mod },
                GrantsAbility = grantsAbility
            });
        }

        private static void AddKeystoneNode(string id, string name, string desc, Vector2 pos,
            string[] connections, TalentModifier[] mods, string grantsAbility = null)
        {
            AddNode(new TalentNode
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = TalentNodeType.Keystone,
                Position = pos,
                Connections = connections.ToList(),
                Modifiers = mods.ToList(),
                GrantsAbility = grantsAbility
            });
        }

        #endregion

        #endregion
    }
}
