using System;
using System.Collections.Generic;
using Vital.Core;
using Viking.Data;
using Viking.Talents;

namespace Viking.Core
{
    /// <summary>
    /// Server-side logic for Viking talent system.
    /// All talent allocations MUST go through this class for validation.
    /// </summary>
    public static class VikingServer
    {
        /// <summary>Event fired when a node is allocated.</summary>
        public static event Action<long, string, int> OnNodeAllocated;

        /// <summary>Event fired when a node is deallocated.</summary>
        public static event Action<long, string> OnNodeDeallocated;

        /// <summary>Event fired when talents are reset.</summary>
        public static event Action<long> OnTalentsReset;

        /// <summary>Event fired when starting point is chosen.</summary>
        public static event Action<long, string> OnStartingPointChosen;

        #region Starting Point

        /// <summary>
        /// Choose a starting point for a player.
        /// </summary>
        public static bool ChooseStartingPoint(long playerId, string startingPointId)
        {
            if (!Plugin.IsServer())
            {
                Plugin.Log.LogWarning("ChooseStartingPoint called on client");
                return false;
            }

            var data = VikingDataStore.Get(playerId);
            if (data == null)
            {
                Plugin.Log.LogError($"No data for player {playerId}");
                return false;
            }

            // Can only choose if not already chosen
            if (!string.IsNullOrEmpty(data.StartingPoint))
            {
                Plugin.Log.LogWarning($"Player {playerId} already has starting point: {data.StartingPoint}");
                return false;
            }

            // Validate starting point exists
            var startPoint = TalentTreeManager.GetStartingPoint(startingPointId);
            if (startPoint == null)
            {
                Plugin.Log.LogError($"Invalid starting point: {startingPointId}");
                return false;
            }

            // Set starting point and allocate start node
            data.StartingPoint = startingPointId;
            data.AllocateNode(startPoint.StartNodeId);
            VikingDataStore.MarkDirty(playerId);

            Plugin.Log.LogInfo($"Player {playerId} chose starting point: {startingPointId}");
            OnStartingPointChosen?.Invoke(playerId, startingPointId);
            OnNodeAllocated?.Invoke(playerId, startPoint.StartNodeId, 1);

            return true;
        }

        #endregion

        #region Node Allocation

        /// <summary>
        /// Validate if a player can allocate a point to a node.
        /// </summary>
        public static bool CanAllocateNode(long playerId, string nodeId)
        {
            var data = VikingDataStore.Get(playerId);
            if (data == null) return false;

            // Must have chosen a starting point
            if (string.IsNullOrEmpty(data.StartingPoint))
            {
                Plugin.Log.LogDebug($"Player {playerId} has no starting point");
                return false;
            }

            // Get player's level for available points
            var player = GetPlayerByID(playerId);
            int availablePoints = player != null ? VikingDataStore.GetAvailablePoints(player) : 0;

            // Must have available points
            if (availablePoints <= 0)
            {
                Plugin.Log.LogDebug($"Player {playerId} has no available points");
                return false;
            }

            // Node must exist
            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null)
            {
                Plugin.Log.LogDebug($"Node {nodeId} does not exist");
                return false;
            }

            // Cannot allocate to start nodes directly (they're auto-allocated)
            if (node.Type == TalentNodeType.Start)
            {
                Plugin.Log.LogDebug($"Cannot allocate to start node {nodeId}");
                return false;
            }

            // Node must not be maxed
            int currentRanks = data.GetNodeRanks(nodeId);
            if (currentRanks >= node.MaxRanks)
            {
                Plugin.Log.LogDebug($"Node {nodeId} is maxed ({currentRanks}/{node.MaxRanks})");
                return false;
            }

            // Node must be reachable (connected to an allocated node)
            if (!TalentTreeManager.IsNodeReachable(data, nodeId))
            {
                Plugin.Log.LogDebug($"Node {nodeId} is not reachable");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Allocate a point to a node (server-side).
        /// </summary>
        public static bool AllocateNode(long playerId, string nodeId)
        {
            if (!Plugin.IsServer())
            {
                Plugin.Log.LogWarning("AllocateNode called on client");
                return false;
            }

            if (!CanAllocateNode(playerId, nodeId))
            {
                return false;
            }

            var data = VikingDataStore.Get(playerId);
            data.AllocateNode(nodeId);
            VikingDataStore.MarkDirty(playerId);

            int newRanks = data.GetNodeRanks(nodeId);
            Plugin.Log.LogInfo($"Player {playerId} allocated node {nodeId} (rank {newRanks})");

            OnNodeAllocated?.Invoke(playerId, nodeId, newRanks);

            // Apply modifiers via Prime (if available)
            ApplyNodeModifiers(playerId, nodeId, 1);

            return true;
        }

        #endregion

        #region Backtrack (Undo)

        /// <summary>
        /// Validate if a player can backtrack (undo last allocation).
        /// </summary>
        public static bool CanBacktrack(long playerId)
        {
            var data = VikingDataStore.Get(playerId);
            if (data == null) return false;

            // Must have allocation history
            if (data.AllocationHistory.Count == 0)
            {
                return false;
            }

            // Cannot undo start node
            string lastNode = data.AllocationHistory[data.AllocationHistory.Count - 1];
            var node = TalentTreeManager.GetNode(lastNode);
            if (node != null && node.Type == TalentNodeType.Start)
            {
                return false;
            }

            // Check if removing would orphan other nodes
            return TalentTreeManager.CanDeallocateNode(data, lastNode);
        }

        /// <summary>
        /// Backtrack (undo) the last talent allocation.
        /// </summary>
        public static bool Backtrack(long playerId)
        {
            if (!Plugin.IsServer())
            {
                Plugin.Log.LogWarning("Backtrack called on client");
                return false;
            }

            if (!CanBacktrack(playerId))
            {
                return false;
            }

            var data = VikingDataStore.Get(playerId);
            string removedNode = data.DeallocateLastNode();

            if (removedNode != null)
            {
                VikingDataStore.MarkDirty(playerId);
                Plugin.Log.LogInfo($"Player {playerId} backtracked node {removedNode}");
                OnNodeDeallocated?.Invoke(playerId, removedNode);

                // Remove modifiers via Prime (if available)
                RemoveNodeModifiers(playerId, removedNode, 1);

                return true;
            }

            return false;
        }

        #endregion

        #region Full Reset

        /// <summary>
        /// Fully reset all talents for a player.
        /// </summary>
        public static bool FullReset(long playerId, bool free = false)
        {
            if (!Plugin.IsServer())
            {
                Plugin.Log.LogWarning("FullReset called on client");
                return false;
            }

            var data = VikingDataStore.Get(playerId);
            if (data == null) return false;

            // TODO: Check and consume currency via Tome if not free
            if (!free)
            {
                // For now, allow free respec
                // In future: TomeAPI.RemoveItem(player, "SoulFragment", 50);
            }

            // Store old allocations to remove modifiers
            var oldAllocations = new Dictionary<string, int>(data.AllocatedNodes);

            // Full reset - clear everything including starting point
            // This allows player to choose a new starting point
            data.ResetAllNodes();
            data.StartingPoint = null;

            VikingDataStore.MarkDirty(playerId);
            Plugin.Log.LogInfo($"Player {playerId} reset all talents (including starting point)");

            // Remove all old modifiers
            foreach (var kvp in oldAllocations)
            {
                RemoveNodeModifiers(playerId, kvp.Key, kvp.Value);
            }

            OnTalentsReset?.Invoke(playerId);
            return true;
        }

        #endregion

        #region Ability Bar

        /// <summary>
        /// Set an ability slot.
        /// </summary>
        public static bool SetAbilitySlot(long playerId, int slot, string abilityId)
        {
            Plugin.Log.LogInfo($"SetAbilitySlot: playerId={playerId}, slot={slot}, abilityId={abilityId}");

            if (!Plugin.IsServer())
            {
                Plugin.Log.LogWarning("SetAbilitySlot called on client");
                return false;
            }

            if (slot < 0 || slot >= 8)
            {
                Plugin.Log.LogWarning($"Invalid slot {slot}");
                return false;
            }

            var data = VikingDataStore.Get(playerId);
            if (data == null)
            {
                Plugin.Log.LogWarning($"SetAbilitySlot: No data for player {playerId}");
                return false;
            }

            // Validate player has the ability (if not empty)
            if (!string.IsNullOrEmpty(abilityId))
            {
                if (!HasAbility(playerId, abilityId))
                {
                    Plugin.Log.LogWarning($"Player {playerId} doesn't have ability {abilityId}");
                    // Log what abilities they DO have
                    var abilities = GetUnlockedAbilities(playerId);
                    Plugin.Log.LogWarning($"Player has abilities: {string.Join(", ", abilities)}");
                    return false;
                }
            }

            if (string.IsNullOrEmpty(abilityId))
            {
                data.AbilitySlots.Remove(slot);
                Plugin.Log.LogInfo($"Cleared slot {slot}");
            }
            else
            {
                data.AbilitySlots[slot] = abilityId;
                Plugin.Log.LogInfo($"Set slot {slot} to {abilityId}");
            }

            VikingDataStore.MarkDirty(playerId);
            return true;
        }

        /// <summary>
        /// Check if a player has unlocked an ability.
        /// </summary>
        public static bool HasAbility(long playerId, string abilityId)
        {
            var data = VikingDataStore.Get(playerId);
            if (data == null) return false;

            // Check all allocated nodes for ability grants
            foreach (var nodeId in data.AllocatedNodes.Keys)
            {
                var node = TalentTreeManager.GetNode(nodeId);
                if (node != null && node.GrantsAbility == abilityId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get all unlocked abilities for a player.
        /// </summary>
        public static List<string> GetUnlockedAbilities(long playerId)
        {
            var abilities = new List<string>();
            var data = VikingDataStore.Get(playerId);
            if (data == null) return abilities;

            foreach (var nodeId in data.AllocatedNodes.Keys)
            {
                var node = TalentTreeManager.GetNode(nodeId);
                if (node != null && node.HasAbility && !abilities.Contains(node.GrantsAbility))
                {
                    abilities.Add(node.GrantsAbility);
                }
            }

            return abilities;
        }

        #endregion

        #region Modifier Application

        /// <summary>
        /// Apply modifiers from a node allocation.
        /// </summary>
        private static void ApplyNodeModifiers(long playerId, string nodeId, int ranksAdded)
        {
            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null) return;

            // TODO: Integrate with Prime to apply actual stat modifiers
            // Example:
            // var player = GetPlayerByID(playerId);
            // if (player != null)
            // {
            //     var container = Prime.EntityManager.Instance.GetOrCreate(player);
            //     foreach (var mod in node.Modifiers)
            //     {
            //         container.AddModifier(new Modifier($"viking_{nodeId}_{mod.Stat}", mod.Stat,
            //             (Prime.ModifierType)mod.Type, mod.Value * ranksAdded));
            //     }
            // }

            Plugin.Log.LogDebug($"Would apply modifiers for node {nodeId} x{ranksAdded}");
        }

        /// <summary>
        /// Remove modifiers from a node deallocation.
        /// </summary>
        private static void RemoveNodeModifiers(long playerId, string nodeId, int ranksRemoved)
        {
            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null) return;

            // TODO: Integrate with Prime to remove stat modifiers
            // Example:
            // var player = GetPlayerByID(playerId);
            // if (player != null)
            // {
            //     var container = Prime.EntityManager.Instance.GetOrCreate(player);
            //     foreach (var mod in node.Modifiers)
            //     {
            //         container.RemoveModifier($"viking_{nodeId}_{mod.Stat}");
            //     }
            // }

            Plugin.Log.LogDebug($"Would remove modifiers for node {nodeId} x{ranksRemoved}");
        }

        #endregion

        #region Helpers

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

        #endregion
    }
}
