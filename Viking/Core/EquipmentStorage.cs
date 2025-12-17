using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Viking.Data;

namespace Viking.Core
{
    /// <summary>
    /// Manages equipment and inventory persistence via VitalDataStore.
    /// This ensures server-authoritative storage that persists across logouts.
    /// </summary>
    public class EquipmentStorage : MonoBehaviour
    {
        private static EquipmentStorage _instance;
        public static EquipmentStorage Instance => _instance;

        // Slot mapping for equipment types
        public const int SLOT_HELMET = 0;
        public const int SLOT_CHEST = 1;
        public const int SLOT_LEGS = 2;
        public const int SLOT_SHOULDER = 3;  // Cape
        public const int SLOT_UTILITY = 4;
        public const int SLOT_WEAPON_RIGHT = 5;
        public const int SLOT_WEAPON_LEFT = 6;  // Shield or second weapon
        public const int SLOT_AMMO = 7;

        private Player _player;
        private bool _isLoading = false;

        private void Awake()
        {
            _instance = this;
            _player = GetComponent<Player>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Creates the equipment storage component on the player.
        /// </summary>
        public static void Create(Player player)
        {
            if (player == null) return;
            if (player.GetComponent<EquipmentStorage>() != null) return;

            player.gameObject.AddComponent<EquipmentStorage>();
            Plugin.Log.LogDebug($"EquipmentStorage created for {player.GetPlayerName()}");
        }

        /// <summary>
        /// Gets the slot index for an item type.
        /// </summary>
        public static int GetSlotForItemType(ItemDrop.ItemData.ItemType itemType)
        {
            return itemType switch
            {
                ItemDrop.ItemData.ItemType.Helmet => SLOT_HELMET,
                ItemDrop.ItemData.ItemType.Chest => SLOT_CHEST,
                ItemDrop.ItemData.ItemType.Legs => SLOT_LEGS,
                ItemDrop.ItemData.ItemType.Shoulder => SLOT_SHOULDER,
                ItemDrop.ItemData.ItemType.Utility => SLOT_UTILITY,
                ItemDrop.ItemData.ItemType.OneHandedWeapon => SLOT_WEAPON_RIGHT,
                ItemDrop.ItemData.ItemType.TwoHandedWeapon => SLOT_WEAPON_RIGHT,
                ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft => SLOT_WEAPON_RIGHT,
                ItemDrop.ItemData.ItemType.Bow => SLOT_WEAPON_RIGHT,
                ItemDrop.ItemData.ItemType.Tool => SLOT_WEAPON_RIGHT,
                ItemDrop.ItemData.ItemType.Torch => SLOT_WEAPON_RIGHT,
                ItemDrop.ItemData.ItemType.Shield => SLOT_WEAPON_LEFT,
                ItemDrop.ItemData.ItemType.Ammo => SLOT_AMMO,
                _ => -1
            };
        }

        #region Save/Load via VitalDataStore

        /// <summary>
        /// Saves equipment and inventory to VitalDataStore.
        /// Equipment is saved from EquipmentInventory (separate slots), bag items from main inventory.
        /// Only runs on server.
        /// </summary>
        public void Save(Player player)
        {
            if (player == null) return;

            Plugin.Log.LogInfo($"EquipmentStorage.Save called for {player.GetPlayerName()} (ID: {player.GetPlayerID()})");
            Plugin.Log.LogInfo($"  IsServer: {ZNet.instance?.IsServer()}, IsDedicated: {ZNet.instance?.IsDedicated()}");

            if (!ZNet.instance.IsServer())
            {
                Plugin.Log.LogDebug("EquipmentStorage.Save skipped - not server");
                return;
            }

            try
            {
                var data = VikingDataStore.GetEquipment(player);
                if (data == null)
                {
                    Plugin.Log.LogWarning("Failed to get equipment data for save");
                    return;
                }

                data.Clear();

                // Save equipment from EquipmentInventory (items are in separate slots)
                var equipInv = EquipmentInventory.Instance;
                if (equipInv != null)
                {
                    for (int slot = 0; slot < 8; slot++)
                    {
                        var item = equipInv.GetItemInSlot(slot);
                        if (item != null)
                        {
                            var serialized = SerializeItem(item);
                            if (serialized != null)
                            {
                                data.Equipment[slot] = serialized;
                                Plugin.Log.LogDebug($"Saved equipment slot {slot}: {item.m_shared.m_name}");
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: Save from Humanoid fields if EquipmentInventory not available
                    Plugin.Log.LogWarning("EquipmentInventory not available, saving from Humanoid fields");
                    SaveEquipmentSlot(player, data, "m_helmetItem", SLOT_HELMET);
                    SaveEquipmentSlot(player, data, "m_chestItem", SLOT_CHEST);
                    SaveEquipmentSlot(player, data, "m_legItem", SLOT_LEGS);
                    SaveEquipmentSlot(player, data, "m_shoulderItem", SLOT_SHOULDER);
                    SaveEquipmentSlot(player, data, "m_utilityItem", SLOT_UTILITY);
                    SaveEquipmentSlot(player, data, "m_rightItem", SLOT_WEAPON_RIGHT);
                    SaveEquipmentSlot(player, data, "m_leftItem", SLOT_WEAPON_LEFT);
                    SaveEquipmentSlot(player, data, "m_ammoItem", SLOT_AMMO);
                    SaveEquipmentSlotIfEmpty(player, data, "m_hiddenRightItem", SLOT_WEAPON_RIGHT);
                    SaveEquipmentSlotIfEmpty(player, data, "m_hiddenLeftItem", SLOT_WEAPON_LEFT);
                }

                // Save main inventory (bag items only - equipment is in separate EquipmentInventory)
                var inventory = player.GetInventory();
                if (inventory != null)
                {
                    foreach (var item in inventory.GetAllItems())
                    {
                        var serialized = SerializeItem(item);
                        if (serialized != null)
                        {
                            data.Inventory.Add(serialized);
                        }
                    }
                }

                VikingDataStore.MarkEquipmentDirty(player);
                Plugin.Log.LogInfo($"Saved equipment ({data.GetEquippedCount()} equipped, {data.GetInventoryCount()} bag) for {player.GetPlayerName()}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save equipment: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads equipment and inventory from VitalDataStore.
        /// Equipment goes to EquipmentInventory (separate slots), bag items to main inventory.
        /// </summary>
        public void Load(Player player)
        {
            if (player == null) return;

            Plugin.Log.LogInfo($"EquipmentStorage.Load called for {player.GetPlayerName()} (ID: {player.GetPlayerID()})");

            if (_isLoading)
            {
                Plugin.Log.LogDebug("Load already in progress, skipping");
                return;
            }

            try
            {
                _isLoading = true;

                var data = VikingDataStore.GetEquipment(player);
                if (data == null)
                {
                    Plugin.Log.LogInfo("No equipment data found for player");
                    return;
                }

                Plugin.Log.LogInfo($"Equipment data found: {data.GetEquippedCount()} equipped, {data.GetInventoryCount()} bag items");

                // Check if there's any data to restore
                if (data.GetEquippedCount() == 0 && data.GetInventoryCount() == 0)
                {
                    Plugin.Log.LogInfo("Equipment data is empty, nothing to restore");
                    return;
                }

                var playerInv = player.GetInventory();
                if (playerInv == null)
                {
                    Plugin.Log.LogWarning("Player inventory is null");
                    return;
                }

                var equipInv = EquipmentInventory.Instance;

                // Set processing flag to prevent equip patches from triggering
                equipInv?.SetProcessing(true);

                try
                {
                    // Clear inventories on server
                    if (ZNet.instance.IsServer())
                    {
                        playerInv.RemoveAll();
                        equipInv?.Clear();
                    }

                    // Restore main inventory (bag items)
                    foreach (var serialized in data.Inventory)
                    {
                        var item = DeserializeItem(serialized);
                        if (item != null)
                        {
                            item.m_gridPos = new Vector2i(serialized.GridX, serialized.GridY);

                            if (!playerInv.AddItem(item))
                            {
                                var emptySlot = FindEmptySlot(playerInv);
                                if (emptySlot.x >= 0)
                                {
                                    item.m_gridPos = emptySlot;
                                    playerInv.AddItem(item);
                                }
                                else
                                {
                                    Plugin.Log.LogWarning($"No space in bag for item: {serialized.Prefab}");
                                }
                            }
                        }
                    }

                    // Restore equipment to EquipmentInventory (separate slots)
                    foreach (var kvp in data.Equipment)
                    {
                        int slot = kvp.Key;
                        var serialized = kvp.Value;

                        var item = DeserializeItem(serialized);
                        if (item != null)
                        {
                            // Add to EquipmentInventory at the correct slot
                            if (equipInv != null)
                            {
                                if (equipInv.AddItemDirect(item, slot))
                                {
                                    // IMPORTANT: Get the item BACK from inventory - Valheim may clone it!
                                    // We must use the same reference for both EquipmentInventory AND Humanoid fields
                                    var storedItem = equipInv.GetItemInSlot(slot);
                                    if (storedItem != null)
                                    {
                                        Plugin.Log.LogDebug($"Loaded equipment slot {slot}: {storedItem.m_shared.m_name}");
                                        // Set Humanoid field with the STORED reference for consistency
                                        EquipItemDirect(player, storedItem, slot);
                                    }
                                }
                                else
                                {
                                    Plugin.Log.LogWarning($"Failed to add item to equipment slot {slot}: {serialized.Prefab}");
                                    // Fallback: try to equip original item even if inventory add failed
                                    EquipItemDirect(player, item, slot);
                                }
                            }
                            else
                            {
                                // No equipment inventory - just set Humanoid field
                                EquipItemDirect(player, item, slot);
                            }
                        }
                    }

                    // Update visuals via reflection (method is protected)
                    _setupEquipmentMethod?.Invoke(player, null);

                    Plugin.Log.LogInfo($"Loaded equipment ({data.GetEquippedCount()} equipped, {data.GetInventoryCount()} bag) for {player.GetPlayerName()}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error during load: {ex.Message}");
                }
                finally
                {
                    // Always clear processing flag
                    equipInv?.SetProcessing(false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load equipment: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        #endregion

        #region Item Serialization

        /// <summary>
        /// Serializes an ItemDrop.ItemData to SerializedItem.
        /// </summary>
        private VikingEquipmentData.SerializedItem SerializeItem(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null) return null;

            var serialized = new VikingEquipmentData.SerializedItem
            {
                Prefab = item.m_dropPrefab.name,
                Stack = item.m_stack,
                Durability = item.m_durability,
                Quality = item.m_quality,
                Variant = item.m_variant,
                CrafterId = item.m_crafterID,
                CrafterName = item.m_crafterName ?? "",
                GridX = item.m_gridPos.x,
                GridY = item.m_gridPos.y,
                CustomData = new Dictionary<string, string>()
            };

            // Copy custom data (for Affix mod, etc.)
            if (item.m_customData != null)
            {
                foreach (var kvp in item.m_customData)
                {
                    serialized.CustomData[kvp.Key] = kvp.Value;
                }
            }

            return serialized;
        }

        /// <summary>
        /// Deserializes a SerializedItem back to ItemDrop.ItemData.
        /// </summary>
        private ItemDrop.ItemData DeserializeItem(VikingEquipmentData.SerializedItem serialized)
        {
            if (serialized == null || string.IsNullOrEmpty(serialized.Prefab)) return null;

            try
            {
                // Get the prefab from ObjectDB
                var prefab = ObjectDB.instance?.GetItemPrefab(serialized.Prefab);
                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"Item prefab not found: {serialized.Prefab}");
                    return null;
                }

                // Get ItemDrop component
                var itemDrop = prefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    Plugin.Log.LogWarning($"ItemDrop component not found on prefab: {serialized.Prefab}");
                    return null;
                }

                // Clone the item data
                var item = itemDrop.m_itemData.Clone();
                item.m_dropPrefab = prefab;
                item.m_stack = serialized.Stack;
                item.m_durability = serialized.Durability;
                item.m_quality = serialized.Quality;
                item.m_variant = serialized.Variant;
                item.m_crafterID = serialized.CrafterId;
                item.m_crafterName = serialized.CrafterName;
                item.m_gridPos = new Vector2i(serialized.GridX, serialized.GridY);

                // Restore custom data
                if (serialized.CustomData != null && serialized.CustomData.Count > 0)
                {
                    item.m_customData ??= new Dictionary<string, string>();
                    foreach (var kvp in serialized.CustomData)
                    {
                        item.m_customData[kvp.Key] = kvp.Value;
                    }
                }

                return item;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to deserialize item {serialized.Prefab}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Equipment Slot Helpers

        private void SaveEquipmentSlot(Player player, VikingEquipmentData data, string fieldName, int slot)
        {
            var item = GetHumanoidEquipmentField(player, fieldName);
            if (item != null)
            {
                var serialized = SerializeItem(item);
                if (serialized != null)
                {
                    data.Equipment[slot] = serialized;
                }
            }
        }

        private void SaveEquipmentSlotIfEmpty(Player player, VikingEquipmentData data, string fieldName, int slot)
        {
            // Only save hidden item if the slot is empty
            if (data.Equipment.ContainsKey(slot)) return;
            SaveEquipmentSlot(player, data, fieldName, slot);
        }

        private ItemDrop.ItemData FindItemInInventory(Inventory inventory, string prefabName, int quality)
        {
            foreach (var item in inventory.GetAllItems())
            {
                if (item.m_dropPrefab != null &&
                    item.m_dropPrefab.name == prefabName &&
                    item.m_quality == quality)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds an empty slot in the inventory.
        /// </summary>
        private Vector2i FindEmptySlot(Inventory inventory)
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

        private bool IsItemEquipped(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null) return false;

            var helmet = GetHumanoidEquipmentField(player, "m_helmetItem");
            var chest = GetHumanoidEquipmentField(player, "m_chestItem");
            var legs = GetHumanoidEquipmentField(player, "m_legItem");
            var shoulder = GetHumanoidEquipmentField(player, "m_shoulderItem");
            var utility = GetHumanoidEquipmentField(player, "m_utilityItem");
            var right = GetHumanoidEquipmentField(player, "m_rightItem");
            var left = GetHumanoidEquipmentField(player, "m_leftItem");
            var ammo = GetHumanoidEquipmentField(player, "m_ammoItem");
            var hiddenRight = GetHumanoidEquipmentField(player, "m_hiddenRightItem");
            var hiddenLeft = GetHumanoidEquipmentField(player, "m_hiddenLeftItem");

            return item == helmet || item == chest || item == legs || item == shoulder ||
                   item == utility || item == right || item == left || item == ammo ||
                   item == hiddenRight || item == hiddenLeft;
        }

        /// <summary>
        /// Directly equips an item by setting the Humanoid field.
        /// </summary>
        private void EquipItemDirect(Player player, ItemDrop.ItemData item, int slot)
        {
            if (player == null || item == null) return;

            string fieldName = slot switch
            {
                SLOT_HELMET => "m_helmetItem",
                SLOT_CHEST => "m_chestItem",
                SLOT_LEGS => "m_legItem",
                SLOT_SHOULDER => "m_shoulderItem",
                SLOT_UTILITY => "m_utilityItem",
                SLOT_WEAPON_RIGHT => "m_rightItem",
                SLOT_WEAPON_LEFT => "m_leftItem",
                SLOT_AMMO => "m_ammoItem",
                _ => null
            };

            if (fieldName != null)
            {
                SetHumanoidEquipmentField(player, fieldName, item);
                item.m_equipped = true;
            }
        }

        #endregion

        #region Reflection Helpers

        private static readonly Dictionary<string, FieldInfo> _humanoidFields = new();
        private static readonly MethodInfo _setupEquipmentMethod = typeof(Humanoid).GetMethod(
            "SetupEquipment",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static ItemDrop.ItemData GetHumanoidEquipmentField(Humanoid humanoid, string fieldName)
        {
            if (!_humanoidFields.TryGetValue(fieldName, out var field))
            {
                field = typeof(Humanoid).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _humanoidFields[fieldName] = field;
            }

            return field?.GetValue(humanoid) as ItemDrop.ItemData;
        }

        private static void SetHumanoidEquipmentField(Humanoid humanoid, string fieldName, ItemDrop.ItemData item)
        {
            if (!_humanoidFields.TryGetValue(fieldName, out var field))
            {
                field = typeof(Humanoid).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _humanoidFields[fieldName] = field;
            }

            field?.SetValue(humanoid, item);
        }

        #endregion
    }
}
