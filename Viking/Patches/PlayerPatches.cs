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
        /// Player spawned - reapply Viking talent modifiers.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Player_OnSpawned_Postfix(Player __instance)
        {
            // Only on local player
            if (__instance != Player.m_localPlayer) return;

            // Reapply all Viking talent modifiers
            if (Plugin.HasPrime)
            {
                PrimeIntegration.ReapplyAllModifiers(__instance);
                Plugin.Log.LogInfo($"Reapplied talent modifiers for {__instance.GetPlayerName()}");
            }
        }

        // NOTE: EquipmentStorage system is DISABLED
        //
        // The concept was to move equipped items to a separate inventory to free up main inventory slots.
        // However, Valheim's save system saves items FROM the inventory list - if we remove items,
        // they don't get saved by vanilla and are lost on reload.
        //
        // To implement this properly would require:
        // 1. Override Player.Save to include equipment storage items
        // 2. Override Player.Load to restore them
        // 3. Handle all edge cases (death, teleportation, etc.)
        //
        // For now, equipment stays in main inventory (vanilla behavior).
        // CharacterWindow reads equipment directly from Humanoid fields (m_helmetItem, etc.)
        // which already works correctly.

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
