using System.Collections.Generic;
using Vital.Core;
using Viking.Data;
using Viking.Network;
using Viking.Talents;

namespace Viking.Core
{
    /// <summary>
    /// Main public API for the Viking talent system.
    /// </summary>
    public static class Viking
    {
        #region Leveling (via Vital)

        /// <summary>
        /// Get player's level.
        /// </summary>
        public static int GetLevel(Player player) => Leveling.GetLevel(player);

        /// <summary>
        /// Get player's total XP.
        /// </summary>
        public static long GetXP(Player player) => Leveling.GetXP(player);

        /// <summary>
        /// Get XP required for next level.
        /// </summary>
        public static long GetXPForNextLevel(Player player)
        {
            int level = GetLevel(player);
            return Leveling.GetXPForLevel(level + 1);
        }

        /// <summary>
        /// Get progress towards next level (0.0 - 1.0).
        /// </summary>
        public static float GetLevelProgress(Player player) => Leveling.GetLevelProgress(player);

        #endregion

        #region Points

        /// <summary>
        /// Get available talent points.
        /// </summary>
        public static int GetAvailablePoints(Player player) => VikingDataStore.GetAvailablePoints(player);

        /// <summary>
        /// Get spent talent points.
        /// </summary>
        public static int GetSpentPoints(Player player) => VikingDataStore.GetSpentPoints(player);

        #endregion

        #region Starting Point

        /// <summary>
        /// Get player's chosen starting point.
        /// </summary>
        public static string GetStartingPoint(Player player)
        {
            var data = VikingDataStore.Get(player);
            return data?.StartingPoint ?? "";
        }

        /// <summary>
        /// Check if player has chosen a starting point.
        /// </summary>
        public static bool HasStartingPoint(Player player)
        {
            return !string.IsNullOrEmpty(GetStartingPoint(player));
        }

        /// <summary>
        /// Choose a starting point (sends request to server).
        /// </summary>
        public static void ChooseStartingPoint(string startingPointId)
        {
            VikingNetwork.RequestChooseStart(startingPointId);
        }

        /// <summary>
        /// Get all available starting points.
        /// </summary>
        public static IEnumerable<StartingPoint> GetAllStartingPoints()
        {
            return TalentTreeManager.GetAllStartingPoints();
        }

        #endregion

        #region Talent Tree

        /// <summary>
        /// Request to allocate a talent node.
        /// </summary>
        public static void RequestAllocateNode(string nodeId)
        {
            VikingNetwork.RequestAllocateNode(nodeId);
        }

        /// <summary>
        /// Check if player can allocate to a node.
        /// </summary>
        public static bool CanAllocate(Player player, string nodeId)
        {
            if (player == null) return false;
            return VikingServer.CanAllocateNode(player.GetPlayerID(), nodeId);
        }

        /// <summary>
        /// Get all allocated nodes for a player.
        /// </summary>
        public static Dictionary<string, int> GetAllocatedNodes(Player player)
        {
            var data = VikingDataStore.Get(player);
            return data?.AllocatedNodes ?? new Dictionary<string, int>();
        }

        /// <summary>
        /// Get ranks allocated to a specific node.
        /// </summary>
        public static int GetNodeRanks(Player player, string nodeId)
        {
            var data = VikingDataStore.Get(player);
            return data?.GetNodeRanks(nodeId) ?? 0;
        }

        /// <summary>
        /// Check if a node has any points allocated.
        /// </summary>
        public static bool HasNode(Player player, string nodeId)
        {
            var data = VikingDataStore.Get(player);
            return data?.HasNode(nodeId) ?? false;
        }

        /// <summary>
        /// Get a talent node by ID.
        /// </summary>
        public static TalentNode GetNode(string nodeId)
        {
            return TalentTreeManager.GetNode(nodeId);
        }

        /// <summary>
        /// Get all talent nodes.
        /// </summary>
        public static IEnumerable<TalentNode> GetAllNodes()
        {
            return TalentTreeManager.GetAllNodes();
        }

        #endregion

        #region Respec

        /// <summary>
        /// Request to backtrack (undo last allocation).
        /// </summary>
        public static void RequestBacktrack()
        {
            VikingNetwork.RequestBacktrack();
        }

        /// <summary>
        /// Request full talent reset.
        /// </summary>
        public static void RequestFullReset()
        {
            VikingNetwork.RequestReset();
        }

        /// <summary>
        /// Check if player can backtrack.
        /// </summary>
        public static bool CanBacktrack(Player player)
        {
            if (player == null) return false;
            return VikingServer.CanBacktrack(player.GetPlayerID());
        }

        #endregion

        #region Abilities

        /// <summary>
        /// Check if player has unlocked an ability.
        /// </summary>
        public static bool HasAbility(Player player, string abilityId)
        {
            if (player == null) return false;
            return VikingServer.HasAbility(player.GetPlayerID(), abilityId);
        }

        /// <summary>
        /// Get all unlocked abilities for a player.
        /// </summary>
        public static List<string> GetUnlockedAbilities(Player player)
        {
            if (player == null) return new List<string>();
            return VikingServer.GetUnlockedAbilities(player.GetPlayerID());
        }

        #endregion

        #region Ability Bar

        /// <summary>
        /// Get ability in a slot.
        /// </summary>
        public static string GetAbilitySlot(Player player, int slot)
        {
            return AbilityBar.GetSlot(player, slot);
        }

        /// <summary>
        /// Set ability slot (sends request to server).
        /// </summary>
        public static void SetAbilitySlot(int slot, string abilityId)
        {
            AbilityBar.SetSlot(slot, abilityId);
        }

        /// <summary>
        /// Use ability slot (cast ability).
        /// </summary>
        public static void UseAbilitySlot(Player player, int slot)
        {
            AbilityBar.UseSlot(player, slot);
        }

        #endregion
    }
}
