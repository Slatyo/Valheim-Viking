using HarmonyLib;
using Viking.Core;

namespace Viking.Patches
{
    /// <summary>
    /// Harmony patches for Inventory methods to support separate equipment storage.
    /// This allows equipped items to be stored in EquipmentInventory while vanilla
    /// code still thinks they're in the player's main inventory.
    /// </summary>
    [HarmonyPatch]
    public static class InventoryPatches
    {
        /// <summary>
        /// Patch ContainsItem to also check EquipmentInventory.
        /// This is crucial - vanilla code checks if items are in inventory before allowing operations.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsItem))]
        [HarmonyPostfix]
        public static void Inventory_ContainsItem_Postfix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            // If already found, no need to check further
            if (__result) return;
            if (item == null) return;

            // Only check EquipmentInventory for the local player's main inventory
            var player = Player.m_localPlayer;
            if (player == null) return;

            // Check if this is the player's main inventory being queried
            if (__instance != player.GetInventory()) return;

            // Check EquipmentInventory
            var equipInv = EquipmentInventory.Instance;
            if (equipInv != null && equipInv.Inventory != null)
            {
                if (equipInv.Inventory.ContainsItem(item))
                {
                    __result = true;
                }
            }
        }

        /// <summary>
        /// Patch GetItem to also check EquipmentInventory.
        /// Used when looking up items by name in inventory.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItem), typeof(string), typeof(int), typeof(bool))]
        [HarmonyPostfix]
        public static void Inventory_GetItem_Postfix(Inventory __instance, string name, int quality, bool isPrefabName, ref ItemDrop.ItemData __result)
        {
            // If already found, no need to check further
            if (__result != null) return;

            // Only check EquipmentInventory for the local player's main inventory
            var player = Player.m_localPlayer;
            if (player == null) return;
            if (__instance != player.GetInventory()) return;

            // Check EquipmentInventory
            var equipInv = EquipmentInventory.Instance;
            if (equipInv != null && equipInv.Inventory != null)
            {
                __result = equipInv.Inventory.GetItem(name, quality, isPrefabName);
            }
        }

        /// <summary>
        /// Patch HaveItem to also check EquipmentInventory.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
        [HarmonyPostfix]
        public static void Inventory_HaveItem_Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
        {
            // If already found, no need to check further
            if (__result) return;

            // Only check EquipmentInventory for the local player's main inventory
            var player = Player.m_localPlayer;
            if (player == null) return;
            if (__instance != player.GetInventory()) return;

            // Check EquipmentInventory
            var equipInv = EquipmentInventory.Instance;
            if (equipInv != null && equipInv.Inventory != null)
            {
                __result = equipInv.Inventory.HaveItem(name, matchWorldLevel);
            }
        }
    }
}
