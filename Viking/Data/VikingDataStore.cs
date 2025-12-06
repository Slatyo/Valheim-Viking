using System;
using Vital.Core;
using Vital.Data;

namespace Viking.Data
{
    /// <summary>
    /// Viking-specific data storage wrapper around VitalDataStore.
    /// </summary>
    public static class VikingDataStore
    {
        public const string MODULE_ID = "viking";

        /// <summary>
        /// Initialize the Viking data module.
        /// </summary>
        internal static void Initialize()
        {
            VitalDataStore.Register<VikingPlayerData>(MODULE_ID);
            Plugin.Log.LogInfo("Viking data module registered");
        }

        /// <summary>
        /// Get Viking data for a player.
        /// </summary>
        public static VikingPlayerData Get(Player player)
        {
            if (player == null) return null;
            return VitalDataStore.Get<VikingPlayerData>(player, MODULE_ID);
        }

        /// <summary>
        /// Get Viking data for a player by ID.
        /// </summary>
        public static VikingPlayerData Get(long playerId)
        {
            return VitalDataStore.Get<VikingPlayerData>(playerId, MODULE_ID);
        }

        /// <summary>
        /// Mark data as dirty (triggers sync).
        /// </summary>
        public static void MarkDirty(Player player)
        {
            VitalDataStore.MarkDirty(player, MODULE_ID);
        }

        /// <summary>
        /// Mark data as dirty by player ID.
        /// </summary>
        public static void MarkDirty(long playerId)
        {
            VitalDataStore.MarkDirty(playerId, MODULE_ID);
        }

        /// <summary>
        /// Get available talent points for a player.
        /// Points = Level - SpentPoints
        /// </summary>
        public static int GetAvailablePoints(Player player)
        {
            if (player == null) return 0;

            int level = Leveling.GetLevel(player);
            var data = Get(player);
            int spent = data?.SpentPoints ?? 0;

            return Math.Max(0, level - spent);
        }

        /// <summary>
        /// Get spent points for a player.
        /// </summary>
        public static int GetSpentPoints(Player player)
        {
            var data = Get(player);
            return data?.SpentPoints ?? 0;
        }
    }
}
