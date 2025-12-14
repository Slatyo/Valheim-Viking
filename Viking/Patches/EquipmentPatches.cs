using HarmonyLib;
using Viking.Core;

namespace Viking.Patches
{
    /// <summary>
    /// Harmony patches for Humanoid equipment methods.
    /// Moves items between main inventory and equipment inventory on equip/unequip.
    /// </summary>
    [HarmonyPatch]
    public static class EquipmentPatches
    {
        /// <summary>
        /// After vanilla equips an item, move it to equipment inventory.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        [HarmonyPostfix]
        public static void Humanoid_EquipItem_Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            // Only for local player
            if (__instance != Player.m_localPlayer) return;

            // Only if equip succeeded
            if (!__result) return;
            if (item == null) return;

            var equipInv = EquipmentInventory.Instance;
            if (equipInv == null) return;

            // Don't process if we're in the middle of loading
            if (equipInv.IsProcessing) return;

            equipInv.OnItemEquipped(item);
        }

        /// <summary>
        /// Before vanilla unequips an item, move it back to bag.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        [HarmonyPrefix]
        public static void Humanoid_UnequipItem_Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            // Only for local player
            if (__instance != Player.m_localPlayer) return;
            if (item == null) return;

            var equipInv = EquipmentInventory.Instance;
            if (equipInv == null) return;

            // Don't process if we're in the middle of loading
            if (equipInv.IsProcessing) return;

            equipInv.OnItemUnequipping(item);
        }

        /// <summary>
        /// Handle holster - weapon moves to hidden slot but stays in equipment inventory.
        /// No inventory changes needed.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.HideHandItems))]
        [HarmonyPostfix]
        public static void Humanoid_HideHandItems_Postfix(Humanoid __instance)
        {
            // No inventory changes needed - item stays in equipment inventory
            // Just the Humanoid field changes (m_rightItem -> m_hiddenRightItem)
            // This is logged for debugging
            if (__instance == Player.m_localPlayer)
            {
                Plugin.Log.LogDebug("Weapons holstered - items remain in equipment inventory");
            }
        }

        /// <summary>
        /// Handle unholster - weapon moves back to visible slot.
        /// No inventory changes needed.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.ShowHandItems))]
        [HarmonyPostfix]
        public static void Humanoid_ShowHandItems_Postfix(Humanoid __instance)
        {
            // No inventory changes needed - item stays in equipment inventory
            if (__instance == Player.m_localPlayer)
            {
                Plugin.Log.LogDebug("Weapons unholstered - items remain in equipment inventory");
            }
        }
    }
}
