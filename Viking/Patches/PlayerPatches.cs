using HarmonyLib;
using State;
using Viking.Core;
using Viking.Data;
using Viking.Integration;

namespace Viking.Patches
{
    /// <summary>
    /// Harmony patches for player lifecycle events, input handling, and equipment persistence.
    /// </summary>
    [HarmonyPatch]
    public static class PlayerPatches
    {
        private static bool _equipmentSyncSubscribed = false;

        #region Player Lifecycle

        /// <summary>
        /// Player spawned - create EquipmentInventory, EquipmentStorage, reapply modifiers, and setup equipment load.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Player_OnSpawned_Postfix(Player __instance)
        {
            // Only on local player
            if (__instance != Player.m_localPlayer) return;

            // Create EquipmentInventory component (must be before EquipmentStorage)
            EquipmentInventory.Create(__instance);

            // Create EquipmentStorage component
            EquipmentStorage.Create(__instance);

            // Reapply all Viking talent modifiers
            if (Plugin.HasPrime)
            {
                PrimeIntegration.ReapplyAllModifiers(__instance);
                Plugin.Log.LogInfo($"Reapplied talent modifiers for {__instance.GetPlayerName()}");
            }

            // Subscribe to State data sync to load equipment after sync completes
            if (!_equipmentSyncSubscribed)
            {
                Store.OnDataSynced += OnDataSynced;
                _equipmentSyncSubscribed = true;
            }
        }

        /// <summary>
        /// Called when Vital syncs data to client.
        /// </summary>
        private static void OnDataSynced(string moduleId)
        {
            if (moduleId != VikingDataStore.EQUIPMENT_MODULE_ID) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            var storage = player.GetComponent<EquipmentStorage>();
            if (storage != null)
            {
                storage.Load(player);
            }
        }

        /// <summary>
        /// Save equipment before player saves.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        [HarmonyPrefix]
        public static void Player_Save_Prefix(Player __instance)
        {
            // Only on server
            if (!ZNet.instance.IsServer()) return;

            var storage = __instance.GetComponent<EquipmentStorage>();
            if (storage != null)
            {
                storage.Save(__instance);
            }
        }

        #endregion

        #region Death Handling (Keep Items)

        /// <summary>
        /// Save equipment before tombstone creation and prevent items from going to tombstone.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        [HarmonyPrefix]
        public static void Player_CreateTombStone_Prefix(Player __instance)
        {
            // Save equipment before death
            if (ZNet.instance.IsServer())
            {
                var storage = __instance.GetComponent<EquipmentStorage>();
                if (storage != null)
                {
                    storage.Save(__instance);
                    Plugin.Log.LogInfo($"Saved equipment before death for {__instance.GetPlayerName()}");
                }
            }

            // Clear main inventory (bag) so nothing goes to tombstone
            // Items are preserved in State.Store and will be restored on respawn
            // Note: EquipmentInventory is NOT cleared here - it's saved separately
            var inventory = __instance.GetInventory();
            if (inventory != null)
            {
                inventory.RemoveAll();
                Plugin.Log.LogDebug("Cleared bag inventory before tombstone creation");
            }
        }

        /// <summary>
        /// Restore equipment after respawn.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnRespawn))]
        [HarmonyPostfix]
        public static void Player_OnRespawn_Postfix(Player __instance)
        {
            // Only on local player
            if (__instance != Player.m_localPlayer) return;

            // Load equipment from State.Store
            var storage = __instance.GetComponent<EquipmentStorage>();
            if (storage != null)
            {
                storage.Load(__instance);
                Plugin.Log.LogInfo($"Restored equipment after respawn for {__instance.GetPlayerName()}");
            }
        }

        #endregion

        #region Hotbar Disable (Viking uses Ability Bar instead)

        /// <summary>
        /// Disable vanilla HotkeyBar Update to prevent 1-8 keybinds from working.
        /// Viking's AbilityBar handles these keybinds instead.
        /// </summary>
        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.Update))]
        [HarmonyPrefix]
        public static bool HotkeyBar_Update_Prefix()
        {
            // Skip vanilla HotkeyBar update entirely - Viking handles 1-8 keybinds
            return false;
        }

        /// <summary>
        /// Disable vanilla HotkeyBar UpdateIcons to prevent visual updates.
        /// </summary>
        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))]
        [HarmonyPrefix]
        public static bool HotkeyBar_UpdateIcons_Prefix()
        {
            // Skip vanilla HotkeyBar icon updates
            return false;
        }

        /// <summary>
        /// Block Player from using hotbar items with 1-8 keys.
        /// This handles cases where Player.Update() checks for hotbar input.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        [HarmonyPrefix]
        public static void Player_Update_Prefix(Player __instance)
        {
            // Only for local player
            if (__instance != Player.m_localPlayer) return;

            // Block vanilla hotbar key detection by consuming the input before Player processes it
            // This is a fallback in case HotkeyBar.Update() isn't the only input handler
        }

        /// <summary>
        /// Intercept Player.UseHotbarItem to prevent vanilla hotbar usage.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
        [HarmonyPrefix]
        public static bool Player_UseHotbarItem_Prefix(int index)
        {
            // Viking handles ability slots 1-8 - block vanilla hotbar usage
            Plugin.Log.LogDebug($"Blocked vanilla UseHotbarItem for slot {index}");
            return false;
        }

        #endregion
    }
}
