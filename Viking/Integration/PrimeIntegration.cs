using System;
using System.Collections.Generic;
using Prime;
using Prime.Abilities;
using Prime.Combat;
using Prime.Core;
using Prime.Events;
using Prime.Modifiers;
using UnityEngine;
using Vital.Core;
using Viking.Core;
using Viking.Data;
using Viking.Talents;

namespace Viking.Integration
{
    /// <summary>
    /// Integration with Prime for stat modifiers, XP from kills, and combat feedback.
    /// </summary>
    public static class PrimeIntegration
    {
        private const string MODIFIER_SOURCE = "Viking";

        /// <summary>
        /// Initialize Prime integration.
        /// </summary>
        public static void Initialize()
        {
            // Subscribe to Prime kill events for XP granting
            PrimeEvents.OnKill += OnKill;

            // Subscribe to Prime combat events for floating damage text
            PrimeEvents.OnPostDamage += OnPostDamage;
            PrimeEvents.OnCritical += OnCritical;
            PrimeEvents.OnBlock += OnBlock;

            // Subscribe to Viking events for modifier application
            VikingServer.OnNodeAllocated += OnNodeAllocated;
            VikingServer.OnNodeDeallocated += OnNodeDeallocated;
            VikingServer.OnTalentsReset += OnTalentsReset;

            Plugin.Log.LogInfo("Prime integration initialized");
        }

        /// <summary>
        /// Cleanup Prime integration.
        /// </summary>
        public static void Cleanup()
        {
            PrimeEvents.OnKill -= OnKill;
            PrimeEvents.OnPostDamage -= OnPostDamage;
            PrimeEvents.OnCritical -= OnCritical;
            PrimeEvents.OnBlock -= OnBlock;
            VikingServer.OnNodeAllocated -= OnNodeAllocated;
            VikingServer.OnNodeDeallocated -= OnNodeDeallocated;
            VikingServer.OnTalentsReset -= OnTalentsReset;
        }

        /// <summary>
        /// Called when a creature is killed - grant XP to the killer.
        /// </summary>
        private static void OnKill(Character killer, Character victim, Prime.Combat.DamageInfo damageInfo)
        {
            // Only server grants XP
            if (!Plugin.IsServer()) return;

            // Only players get XP
            if (killer is not Player player) return;

            // Get victim level (from Vital if available, otherwise estimate)
            int victimLevel = GetCreatureLevel(victim);

            // Calculate XP using Vital's formula
            long xp = Leveling.GetKillXP(victimLevel, isElite: false, isBoss: victim.IsBoss());

            // Grant XP through Vital
            Leveling.AddXP(player, xp);

            Plugin.Log.LogDebug($"Granted {xp} XP to {player.GetPlayerName()} for killing level {victimLevel} creature");

            // Show XP floating text if Veneer is available
            if (Plugin.HasVeneer)
            {
                ShowXPText(xp, player.transform.position);
            }
        }

        /// <summary>
        /// Called after damage is calculated - show floating damage text.
        /// Shows EACH damage type separately for visual clarity.
        /// </summary>
        private static void OnPostDamage(DamageInfo damageInfo)
        {
            if (damageInfo == null || damageInfo.Target == null) return;
            if (damageInfo.FinalDamage <= 0) return;

            // Only show on client (visual feedback)
            if (!Plugin.HasVeneer) return;

            // Get base hit position for floating text
            Vector3 basePosition = damageInfo.HitPoint;
            if (basePosition == Vector3.zero && damageInfo.Target != null)
            {
                basePosition = damageInfo.Target.transform.position + Vector3.up * 1.5f;
            }

            // Check if local player is taking damage
            bool isDamageTaken = damageInfo.Target == Player.m_localPlayer;

            // Crit is handled separately by OnCritical, so skip if crit
            if (damageInfo.IsCritical) return;

            // Show EACH damage type separately
            int index = 0;
            foreach (var kvp in damageInfo.Damages)
            {
                if (kvp.Value <= 0) continue;

                // Offset each number slightly so they don't overlap
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-0.3f, 0.3f),
                    0.3f * index,
                    UnityEngine.Random.Range(-0.3f, 0.3f)
                );

                string damageTypeName = GetDamageTypeName(kvp.Key);
                ShowDamageText(kvp.Value, basePosition + offset, false, damageTypeName, isDamageTaken);
                index++;
            }
        }

        /// <summary>
        /// Called on critical hits - show special crit text for EACH damage type.
        /// </summary>
        private static void OnCritical(DamageInfo damageInfo)
        {
            if (damageInfo == null || damageInfo.Target == null) return;
            if (!Plugin.HasVeneer) return;

            Vector3 basePosition = damageInfo.HitPoint;
            if (basePosition == Vector3.zero && damageInfo.Target != null)
            {
                basePosition = damageInfo.Target.transform.position + Vector3.up * 1.5f;
            }

            // Show EACH damage type separately with crit styling
            int index = 0;
            foreach (var kvp in damageInfo.Damages)
            {
                if (kvp.Value <= 0) continue;

                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-0.3f, 0.3f),
                    0.3f * index,
                    UnityEngine.Random.Range(-0.3f, 0.3f)
                );

                // Crits show in gold/yellow regardless of damage type
                ShowDamageText(kvp.Value, basePosition + offset, true, null);
                index++;
            }
        }

        /// <summary>
        /// Called when damage is blocked - show blocked text.
        /// </summary>
        private static void OnBlock(Character blocker, Character attacker, DamageInfo damageInfo)
        {
            if (blocker == null || damageInfo == null) return;
            if (!Plugin.HasVeneer) return;

            Vector3 position = blocker.transform.position + Vector3.up * 1.5f;
            ShowBlockedText(damageInfo.BlockedAmount, position);
        }

        /// <summary>
        /// Get damage type name string for coloring.
        /// </summary>
        private static string GetDamageTypeName(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => "fire",
                DamageType.Frost => "frost",
                DamageType.Lightning => "lightning",
                DamageType.Poison => "poison",
                DamageType.Spirit => "spirit",
                _ => null // Physical types show as white/normal
            };
        }

        /// <summary>
        /// Get the primary damage type for coloring (legacy - kept for compatibility).
        /// </summary>
        private static string GetPrimaryDamageType(DamageInfo damageInfo)
        {
            float maxDamage = 0f;
            string primaryType = null;

            foreach (var kvp in damageInfo.Damages)
            {
                if (kvp.Value > maxDamage)
                {
                    maxDamage = kvp.Value;
                    primaryType = GetDamageTypeName(kvp.Key);
                }
            }

            return primaryType;
        }

        #region Veneer Floating Text (called if Veneer available)

        private static void ShowDamageText(float damage, Vector3 position, bool isCritical, string damageType, bool isDamageTaken = false)
        {
            try
            {
                Veneer.Core.VeneerAPI.ShowDamageText(damage, position, isCritical, damageType, isDamageTaken);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to show damage text: {ex.Message}");
            }
        }

        private static void ShowXPText(long amount, Vector3 position)
        {
            try
            {
                Veneer.Core.VeneerAPI.ShowXPText(amount, position);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to show XP text: {ex.Message}");
            }
        }

        private static void ShowBlockedText(float amount, Vector3 position)
        {
            try
            {
                Veneer.Core.VeneerAPI.ShowBlockedText(amount, position);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to show blocked text: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Get creature level (uses Vital if it has level data, otherwise estimates from health).
        /// </summary>
        private static int GetCreatureLevel(Character creature)
        {
            // Try to get from Vital first
            int level = Leveling.GetLevel(creature);
            if (level > 0) return level;

            // Fallback: estimate from max health
            float maxHealth = creature.GetMaxHealth();
            if (maxHealth <= 25) return 1;
            if (maxHealth <= 50) return 5;
            if (maxHealth <= 100) return 10;
            if (maxHealth <= 200) return 20;
            if (maxHealth <= 500) return 35;
            if (maxHealth <= 1000) return 50;
            return 75;
        }

        /// <summary>
        /// Called when a talent node is allocated - apply modifiers and grant abilities.
        /// </summary>
        private static void OnNodeAllocated(long playerId, string nodeId, int newRank)
        {
            var player = GetPlayerByID(playerId);
            if (player == null) return;

            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null) return;

            ApplyNodeModifiers(player, node, 1);

            // Grant ability if this node grants one (only on first rank)
            if (newRank == 1 && node.HasAbility)
            {
                if (PrimeAPI.GrantAbility(player, node.GrantsAbility))
                {
                    Plugin.Log.LogInfo($"Granted ability '{node.GrantsAbility}' from node {nodeId} to {player.GetPlayerName()}");
                }
                else
                {
                    Plugin.Log.LogWarning($"Failed to grant ability '{node.GrantsAbility}' - ability may not be registered in Prime");
                }
            }

            Plugin.Log.LogDebug($"Applied modifiers for {nodeId} rank {newRank} to {player.GetPlayerName()}");
        }

        /// <summary>
        /// Called when a talent node is deallocated - remove modifiers and revoke abilities.
        /// </summary>
        private static void OnNodeDeallocated(long playerId, string nodeId)
        {
            var player = GetPlayerByID(playerId);
            if (player == null) return;

            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null) return;

            RemoveNodeModifiers(player, node);

            // Revoke ability if this node granted one
            if (node.HasAbility)
            {
                if (PrimeAPI.RevokeAbility(player, node.GrantsAbility))
                {
                    Plugin.Log.LogInfo($"Revoked ability '{node.GrantsAbility}' from {player.GetPlayerName()}");
                }
            }

            Plugin.Log.LogDebug($"Removed modifiers for {nodeId} from {player.GetPlayerName()}");
        }

        /// <summary>
        /// Called when all talents are reset - remove all Viking modifiers and abilities.
        /// </summary>
        private static void OnTalentsReset(long playerId)
        {
            var player = GetPlayerByID(playerId);
            if (player == null) return;

            RemoveAllVikingModifiers(player);
            RevokeAllVikingAbilities(player);

            // Re-apply start node if player has starting point
            var data = VikingDataStore.Get(player);
            if (data != null && !string.IsNullOrEmpty(data.StartingPoint))
            {
                var startPoint = TalentTreeManager.GetStartingPoint(data.StartingPoint);
                if (startPoint != null)
                {
                    var startNode = TalentTreeManager.GetNode(startPoint.StartNodeId);
                    if (startNode != null)
                    {
                        ApplyNodeModifiers(player, startNode, 1);
                        // Grant ability from start node if it has one
                        if (startNode.HasAbility)
                        {
                            PrimeAPI.GrantAbility(player, startNode.GrantsAbility);
                        }
                    }
                }
            }

            Plugin.Log.LogDebug($"Reset all Viking modifiers and abilities for {player.GetPlayerName()}");
        }

        /// <summary>
        /// Apply modifiers from a talent node to a player.
        /// </summary>
        private static void ApplyNodeModifiers(Player player, TalentNode node, int ranksAdded)
        {
            if (node.Modifiers == null || node.Modifiers.Count == 0) return;

            var container = EntityManager.Instance.GetOrCreate(player);
            Plugin.Log.LogDebug($"[Viking] ApplyNodeModifiers: Player {player.GetPlayerName()}, Node {node.Id}, Container exists: {container != null}");

            foreach (var mod in node.Modifiers)
            {
                string modifierId = $"viking_{node.Id}_{mod.Stat}";

                // Convert our modifier type to Prime's
                ModifierType primeType = mod.Type switch
                {
                    TalentModifierType.Flat => ModifierType.Flat,
                    TalentModifierType.Percent => ModifierType.Percent,
                    TalentModifierType.Multiply => ModifierType.Multiply,
                    _ => ModifierType.Flat
                };

                // Check if modifier already exists (multi-rank node)
                var existingModifiers = container.GetModifiers(mod.Stat);
                Modifier existing = null;
                foreach (var m in existingModifiers)
                {
                    if (m.Id == modifierId)
                    {
                        existing = m;
                        break;
                    }
                }

                if (existing != null)
                {
                    // Update value for additional rank
                    existing.Value += mod.Value;
                    if (mod.Stat == "MaxHealth" || mod.Stat == "MaxStamina")
                    {
                        Plugin.Log.LogDebug($"[Viking] Updated modifier {modifierId}: {mod.Stat} now = {existing.Value}");
                    }
                }
                else
                {
                    // Create new modifier
                    var primeModifier = new Modifier(modifierId, mod.Stat, primeType, mod.Value * ranksAdded)
                    {
                        Source = MODIFIER_SOURCE,
                        Order = ModifierOrder.Default
                    };
                    container.AddModifier(primeModifier);
                    if (mod.Stat == "MaxHealth" || mod.Stat == "MaxStamina")
                    {
                        Plugin.Log.LogDebug($"[Viking] Added modifier {modifierId}: {mod.Stat} {primeType} {mod.Value * ranksAdded}");
                    }
                }
            }
        }

        /// <summary>
        /// Remove modifiers from a talent node from a player.
        /// </summary>
        private static void RemoveNodeModifiers(Player player, TalentNode node)
        {
            if (node.Modifiers == null || node.Modifiers.Count == 0) return;

            var container = EntityManager.Instance.Get(player);
            if (container == null) return;

            foreach (var mod in node.Modifiers)
            {
                string modifierId = $"viking_{node.Id}_{mod.Stat}";
                container.RemoveModifier(modifierId);
            }
        }

        /// <summary>
        /// Remove all Viking modifiers from a player.
        /// </summary>
        private static void RemoveAllVikingModifiers(Player player)
        {
            var container = EntityManager.Instance.Get(player);
            if (container == null) return;

            container.RemoveModifiersFromSource(MODIFIER_SOURCE);
        }

        /// <summary>
        /// Revoke all abilities granted by Viking talent nodes.
        /// </summary>
        private static void RevokeAllVikingAbilities(Player player)
        {
            var data = VikingDataStore.Get(player);
            if (data == null) return;

            foreach (var nodeId in data.AllocatedNodes.Keys)
            {
                var node = TalentTreeManager.GetNode(nodeId);
                if (node != null && node.HasAbility)
                {
                    PrimeAPI.RevokeAbility(player, node.GrantsAbility);
                }
            }
        }

        /// <summary>
        /// Reapply all modifiers and abilities for a player (e.g., on login).
        /// </summary>
        public static void ReapplyAllModifiers(Player player)
        {
            if (player == null) return;

            var data = VikingDataStore.Get(player);
            if (data == null)
            {
                Plugin.Log.LogDebug($"[Viking] ReapplyAllModifiers: No data for player {player.GetPlayerName()}");
                return;
            }

            Plugin.Log.LogInfo($"[Viking] ReapplyAllModifiers: Player {player.GetPlayerName()}, {data.AllocatedNodes.Count} nodes allocated");

            // Remove existing Viking modifiers and abilities
            RemoveAllVikingModifiers(player);
            RevokeAllVikingAbilities(player);

            // Reapply all allocated nodes
            foreach (var kvp in data.AllocatedNodes)
            {
                var node = TalentTreeManager.GetNode(kvp.Key);
                if (node != null)
                {
                    ApplyNodeModifiers(player, node, kvp.Value);

                    // Grant ability if node has one (only if at least 1 rank)
                    if (kvp.Value >= 1 && node.HasAbility)
                    {
                        PrimeAPI.GrantAbility(player, node.GrantsAbility);
                    }
                }
            }

            Plugin.Log.LogDebug($"Reapplied all Viking modifiers and abilities for {player.GetPlayerName()}");
        }

        /// <summary>
        /// Get a Player instance by player ID.
        /// </summary>
        private static Player GetPlayerByID(long playerId)
        {
            foreach (var player in Player.GetAllPlayers())
            {
                if (player.GetPlayerID() == playerId)
                {
                    return player;
                }
            }
            return null;
        }
    }
}
