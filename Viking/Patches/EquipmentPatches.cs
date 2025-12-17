using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Viking.Core;

namespace Viking.Patches
{
    /// <summary>
    /// Harmony patches for Humanoid equipment methods.
    /// Takes FULL control of equip/unequip to properly manage EquipmentInventory.
    /// Items in EquipmentInventory don't use bag space.
    /// </summary>
    [HarmonyPatch]
    public static class EquipmentPatches
    {
        private static readonly Dictionary<string, FieldInfo> _humanoidFields = new();
        private static readonly MethodInfo _setupEquipmentMethod = typeof(Humanoid).GetMethod(
            "SetupEquipment",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo _triggerEquipEffectMethod = typeof(Humanoid).GetMethod(
            "TriggerEquipEffect",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>
        /// FULL CONTROL: Handle all equip logic ourselves, skip vanilla.
        /// This prevents conflicts between our item movement and vanilla's expectations.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        [HarmonyPrefix]
        public static bool Humanoid_EquipItem_Prefix(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects, ref bool __result)
        {
            // Only handle local player
            if (__instance != Player.m_localPlayer)
                return true; // Let vanilla handle non-local

            if (item == null)
            {
                __result = false;
                return false;
            }

            var equipInv = EquipmentInventory.Instance;
            if (equipInv == null)
                return true; // No equipment inventory, let vanilla handle

            // Don't interfere during load
            if (equipInv.IsProcessing)
                return true;

            var player = (Player)__instance;
            var playerInv = player.GetInventory();
            int slot = EquipmentInventory.GetSlotForItemType(item.m_shared.m_itemType);

            if (slot < 0)
            {
                // Not an equippable type we handle
                return true;
            }

            Plugin.Log.LogDebug($"EquipItem: {item.m_shared.m_name} to slot {slot}");

            // Check if item is available (in bag or already in equipment)
            bool inBag = playerInv.ContainsItem(item);
            bool inEquip = equipInv.Inventory.ContainsItem(item);

            Plugin.Log.LogDebug($"  inBag={inBag}, inEquip={inEquip}, equipInvCount={equipInv.Inventory.GetAllItems().Count}");

            // List all items in equipment inventory for debugging
            if (!inEquip)
            {
                var allEquipItems = equipInv.Inventory.GetAllItems();
                Plugin.Log.LogDebug($"  Equipment inventory items ({allEquipItems.Count}):");
                foreach (var eq in allEquipItems)
                {
                    Plugin.Log.LogDebug($"    - {eq.m_shared.m_name} (same ref: {eq == item})");
                }
            }

            if (!inBag && !inEquip)
            {
                Plugin.Log.LogWarning($"Item {item.m_shared.m_name} not found in bag or equipment");
                __result = false;
                return false;
            }

            // Unequip current item in this slot (if any)
            var currentItem = equipInv.GetItemInSlot(slot);
            if (currentItem != null && currentItem != item)
            {
                Plugin.Log.LogDebug($"Unequipping current item in slot {slot}: {currentItem.m_shared.m_name}");

                // Clear the Humanoid field
                ClearHumanoidEquipmentField(__instance, slot);
                currentItem.m_equipped = false;

                // Move current item from EquipmentInventory to bag
                equipInv.Inventory.RemoveItem(currentItem);

                var emptySlot = FindEmptySlot(playerInv);
                if (emptySlot.x >= 0)
                {
                    currentItem.m_gridPos = emptySlot;
                    playerInv.AddItem(currentItem);
                    Plugin.Log.LogDebug($"Moved {currentItem.m_shared.m_name} to bag at ({emptySlot.x}, {emptySlot.y})");
                }
                else
                {
                    // Drop on ground if no space
                    ItemDrop.DropItem(currentItem, currentItem.m_stack, player.transform.position, Quaternion.identity);
                    Plugin.Log.LogWarning($"No bag space, dropped {currentItem.m_shared.m_name}");
                }
            }

            // If item is in bag, move to equipment inventory
            ItemDrop.ItemData itemToEquip = item;
            if (inBag && !inEquip)
            {
                playerInv.RemoveItem(item);
                equipInv.AddItemDirect(item, slot);
                // IMPORTANT: Get the item BACK from inventory - Valheim clones items when adding!
                // We must use the same reference for both EquipmentInventory AND Humanoid fields
                itemToEquip = equipInv.GetItemInSlot(slot) ?? item;
                Plugin.Log.LogDebug($"Moved {itemToEquip.m_shared.m_name} from bag to equipment slot {slot}");
            }
            else if (inEquip)
            {
                // Item already in equipment inventory - get the stored reference
                itemToEquip = equipInv.GetItemInSlot(slot) ?? item;
                Plugin.Log.LogDebug($"Item {itemToEquip.m_shared.m_name} already in equipment inventory");
            }

            // Set the Humanoid equipment field with the STORED item reference
            SetHumanoidEquipmentField(__instance, slot, itemToEquip);
            itemToEquip.m_equipped = true;

            // Setup visuals via reflection (method is protected)
            _setupEquipmentMethod?.Invoke(__instance, null);

            if (triggerEquipEffects)
            {
                _triggerEquipEffectMethod?.Invoke(__instance, new object[] { itemToEquip });
            }

            __result = true;
            return false; // Skip vanilla EquipItem entirely
        }

        /// <summary>
        /// FULL CONTROL: Handle all unequip logic ourselves, skip vanilla.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        [HarmonyPrefix]
        public static bool Humanoid_UnequipItem_Prefix(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            // Only handle local player
            if (__instance != Player.m_localPlayer)
                return true;

            if (item == null)
                return false;

            var equipInv = EquipmentInventory.Instance;
            if (equipInv == null)
                return true;

            // Don't interfere during load
            if (equipInv.IsProcessing)
                return true;

            var player = (Player)__instance;
            int slot = EquipmentInventory.GetSlotForItemType(item.m_shared.m_itemType);

            if (slot < 0)
                return true;

            Plugin.Log.LogDebug($"UnequipItem: {item.m_shared.m_name} from slot {slot}");

            // Clear the Humanoid field
            ClearHumanoidEquipmentField(__instance, slot);
            item.m_equipped = false;

            // If item is in equipment inventory, move to bag
            if (equipInv.Inventory.ContainsItem(item))
            {
                equipInv.Inventory.RemoveItem(item);

                var playerInv = player.GetInventory();
                var emptySlot = FindEmptySlot(playerInv);

                if (emptySlot.x >= 0)
                {
                    item.m_gridPos = emptySlot;
                    playerInv.AddItem(item);
                    Plugin.Log.LogDebug($"Moved {item.m_shared.m_name} to bag at ({emptySlot.x}, {emptySlot.y})");
                }
                else
                {
                    ItemDrop.DropItem(item, item.m_stack, player.transform.position, Quaternion.identity);
                    Plugin.Log.LogWarning($"No bag space, dropped {item.m_shared.m_name}");
                }
            }

            // Setup visuals via reflection (method is protected)
            _setupEquipmentMethod?.Invoke(__instance, null);

            if (triggerEquipEffects)
            {
                // Trigger unequip effects if needed
            }

            return false; // Skip vanilla UnequipItem entirely
        }

        /// <summary>
        /// Handle holster OURSELVES - skip vanilla entirely.
        /// Vanilla's HideHandItems moves items to player inventory - we don't want that.
        /// Items stay in EquipmentInventory, we just move references between Humanoid fields.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.HideHandItems))]
        [HarmonyPrefix]
        public static bool Humanoid_HideHandItems_Prefix(Humanoid __instance)
        {
            // Only handle local player
            if (__instance != Player.m_localPlayer)
                return true;

            var rightItem = GetHumanoidField(__instance, "m_rightItem");
            var leftItem = GetHumanoidField(__instance, "m_leftItem");

            Plugin.Log.LogDebug($"HideHandItems: rightItem={(rightItem?.m_shared.m_name ?? "null")}, leftItem={(leftItem?.m_shared.m_name ?? "null")}");

            // Move right hand item to hidden
            if (rightItem != null)
            {
                SetHumanoidField(__instance, "m_hiddenRightItem", rightItem);
                SetHumanoidField(__instance, "m_rightItem", null);
                // Keep m_equipped = true - item is still equipped, just holstered
            }

            // Move left hand item to hidden
            if (leftItem != null)
            {
                SetHumanoidField(__instance, "m_hiddenLeftItem", leftItem);
                SetHumanoidField(__instance, "m_leftItem", null);
                // Keep m_equipped = true - item is still equipped, just holstered
            }

            // Update visuals
            _setupEquipmentMethod?.Invoke(__instance, null);

            return false; // Skip vanilla HideHandItems entirely
        }

        private static ItemDrop.ItemData GetHumanoidField(Humanoid humanoid, string fieldName)
        {
            var field = GetCachedField(fieldName);
            return field?.GetValue(humanoid) as ItemDrop.ItemData;
        }

        private static void SetHumanoidField(Humanoid humanoid, string fieldName, ItemDrop.ItemData item)
        {
            var field = GetCachedField(fieldName);
            field?.SetValue(humanoid, item);
        }

        /// <summary>
        /// Handle unholster OURSELVES - skip vanilla entirely.
        /// Vanilla's ShowHandItems calls EquipItem which expects items in player inventory.
        /// We just need to move references between Humanoid fields - no inventory lookup needed.
        /// Items stay in EquipmentInventory the whole time.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.ShowHandItems))]
        [HarmonyPrefix]
        public static bool Humanoid_ShowHandItems_Prefix(Humanoid __instance)
        {
            // Only handle local player
            if (__instance != Player.m_localPlayer)
                return true;

            var hiddenRight = GetHumanoidField(__instance, "m_hiddenRightItem");
            var hiddenLeft = GetHumanoidField(__instance, "m_hiddenLeftItem");

            Plugin.Log.LogDebug($"ShowHandItems: hiddenRight={(hiddenRight?.m_shared.m_name ?? "null")}, hiddenLeft={(hiddenLeft?.m_shared.m_name ?? "null")}");

            // Restore right hand item
            if (hiddenRight != null)
            {
                SetHumanoidField(__instance, "m_rightItem", hiddenRight);
                SetHumanoidField(__instance, "m_hiddenRightItem", null);
                // m_equipped should already be true from holster
            }

            // Restore left hand item
            if (hiddenLeft != null)
            {
                SetHumanoidField(__instance, "m_leftItem", hiddenLeft);
                SetHumanoidField(__instance, "m_hiddenLeftItem", null);
                // m_equipped should already be true from holster
            }

            // Update visuals
            _setupEquipmentMethod?.Invoke(__instance, null);

            return false; // Skip vanilla ShowHandItems entirely
        }

        #region Helper Methods

        private static void SetHumanoidEquipmentField(Humanoid humanoid, int slot, ItemDrop.ItemData item)
        {
            string fieldName = GetFieldNameForSlot(slot);
            if (fieldName == null) return;

            var field = GetCachedField(fieldName);
            field?.SetValue(humanoid, item);
        }

        private static void ClearHumanoidEquipmentField(Humanoid humanoid, int slot)
        {
            string fieldName = GetFieldNameForSlot(slot);
            if (fieldName == null) return;

            var field = GetCachedField(fieldName);
            field?.SetValue(humanoid, null);
        }

        private static string GetFieldNameForSlot(int slot)
        {
            return slot switch
            {
                EquipmentInventory.SLOT_HELMET => "m_helmetItem",
                EquipmentInventory.SLOT_CHEST => "m_chestItem",
                EquipmentInventory.SLOT_LEGS => "m_legItem",
                EquipmentInventory.SLOT_SHOULDER => "m_shoulderItem",
                EquipmentInventory.SLOT_UTILITY => "m_utilityItem",
                EquipmentInventory.SLOT_WEAPON_RIGHT => "m_rightItem",
                EquipmentInventory.SLOT_WEAPON_LEFT => "m_leftItem",
                EquipmentInventory.SLOT_AMMO => "m_ammoItem",
                _ => null
            };
        }

        private static FieldInfo GetCachedField(string fieldName)
        {
            if (!_humanoidFields.TryGetValue(fieldName, out var field))
            {
                field = typeof(Humanoid).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _humanoidFields[fieldName] = field;
            }
            return field;
        }

        private static Vector2i FindEmptySlot(Inventory inventory)
        {
            int width = inventory.GetWidth();
            int height = inventory.GetHeight();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        return new Vector2i(x, y);
                    }
                }
            }
            return new Vector2i(-1, -1);
        }

        #endregion
    }
}
