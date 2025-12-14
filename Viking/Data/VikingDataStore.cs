using System;
using State;
using Vital.Core;

namespace Viking.Data
{
    /// <summary>
    /// Viking-specific data storage wrapper around State.Store.
    /// </summary>
    public static class VikingDataStore
    {
        public const string MODULE_ID = "viking";
        public const string EQUIPMENT_MODULE_ID = "viking_equipment";

        /// <summary>
        /// Initialize the Viking data modules.
        /// </summary>
        internal static void Initialize()
        {
            Store.Register<VikingPlayerData>(MODULE_ID);
            Store.Register<VikingEquipmentData>(EQUIPMENT_MODULE_ID);
            Plugin.Log.LogInfo("Viking data modules registered (talents + equipment)");
        }

        /// <summary>
        /// Get Viking data for a player.
        /// </summary>
        public static VikingPlayerData Get(Player player)
        {
            if (player == null) return null;
            return Store.Get<VikingPlayerData>(player, MODULE_ID);
        }

        /// <summary>
        /// Get Viking data for a player by ID.
        /// </summary>
        public static VikingPlayerData Get(long playerId)
        {
            return Store.Get<VikingPlayerData>(playerId, MODULE_ID);
        }

        /// <summary>
        /// Mark data as dirty (triggers sync).
        /// </summary>
        public static void MarkDirty(Player player)
        {
            Store.MarkDirty(player, MODULE_ID);
        }

        /// <summary>
        /// Mark data as dirty by player ID.
        /// </summary>
        public static void MarkDirty(long playerId)
        {
            Store.MarkDirty(playerId, MODULE_ID);
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

        #region Equipment Data Access

        /// <summary>
        /// Get equipment data for a player.
        /// </summary>
        public static VikingEquipmentData GetEquipment(Player player)
        {
            if (player == null) return null;
            return Store.Get<VikingEquipmentData>(player, EQUIPMENT_MODULE_ID);
        }

        /// <summary>
        /// Get equipment data for a player by ID.
        /// </summary>
        public static VikingEquipmentData GetEquipment(long playerId)
        {
            return Store.Get<VikingEquipmentData>(playerId, EQUIPMENT_MODULE_ID);
        }

        /// <summary>
        /// Mark equipment data as dirty (triggers sync).
        /// </summary>
        public static void MarkEquipmentDirty(Player player)
        {
            Store.MarkDirty(player, EQUIPMENT_MODULE_ID);
        }

        /// <summary>
        /// Mark equipment data as dirty by player ID.
        /// </summary>
        public static void MarkEquipmentDirty(long playerId)
        {
            Store.MarkDirty(playerId, EQUIPMENT_MODULE_ID);
        }

        #endregion
    }
}
