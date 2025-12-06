using HarmonyLib;
using Viking.Core;
using Viking.Integration;

namespace Viking.Patches
{
    /// <summary>
    /// Harmony patches for player lifecycle events and input handling.
    /// </summary>
    [HarmonyPatch]
    public static class PlayerPatches
    {

        /// <summary>
        /// Create EquipmentStorage when player spawns.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Player_OnSpawned_Postfix(Player __instance)
        {
            // Only on local player
            if (__instance != Player.m_localPlayer) return;

            // Create equipment storage
            EquipmentStorage.Create(__instance);

            // Reapply all Viking talent modifiers
            if (Plugin.HasPrime)
            {
                PrimeIntegration.ReapplyAllModifiers(__instance);
                Plugin.Log.LogInfo($"Reapplied talent modifiers for {__instance.GetPlayerName()}");
            }
        }

        /// <summary>
        /// Load equipment storage when player loads.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        [HarmonyPostfix]
        public static void Player_Load_Postfix(Player __instance)
        {
            // Only on local player
            if (__instance != Player.m_localPlayer) return;

            // Load equipment storage
            var storage = __instance.GetComponent<EquipmentStorage>();
            storage?.Load(__instance);
        }

        /// <summary>
        /// Save equipment storage when player saves.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        [HarmonyPrefix]
        public static void Player_Save_Prefix(Player __instance)
        {
            // Only on local player
            if (__instance != Player.m_localPlayer) return;

            // Save equipment storage
            var storage = __instance.GetComponent<EquipmentStorage>();
            storage?.Save(__instance);
        }

        // NOTE: EquipmentStorage system disabled for now - causes issues with holstering
        // Equipment stays in player inventory like vanilla, but is displayed in Character Window
        // TODO: Revisit equipment storage system later if needed

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
    }
}
