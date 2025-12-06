using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Viking.Core
{
    /// <summary>
    /// Manages a separate inventory for equipped items.
    /// This frees up main inventory slots when items are equipped.
    /// Equipment is stored in a hidden inventory that persists with the player.
    /// </summary>
    public class EquipmentStorage : MonoBehaviour
    {
        private static EquipmentStorage _instance;
        public static EquipmentStorage Instance => _instance;

        // The hidden inventory for equipped items (8 slots for different equipment types)
        private Inventory _equipmentInventory;

        // Slot mapping for equipment types
        public const int SLOT_HELMET = 0;
        public const int SLOT_CHEST = 1;
        public const int SLOT_LEGS = 2;
        public const int SLOT_SHOULDER = 3;  // Cape
        public const int SLOT_UTILITY = 4;
        public const int SLOT_WEAPON_RIGHT = 5;
        public const int SLOT_WEAPON_LEFT = 6;  // Shield or second weapon
        public const int SLOT_AMMO = 7;

        // Custom save key for equipment inventory
        private const string SAVE_KEY = "VikingEquipment";

        private void Awake()
        {
            _instance = this;
            // Create equipment inventory: 8 slots wide, 1 row
            _equipmentInventory = new Inventory("VikingEquipment", null, 8, 1);
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

        /// <summary>
        /// Moves an item from player inventory to equipment storage.
        /// Called after equipping.
        /// </summary>
        public bool MoveToEquipment(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null || _equipmentInventory == null) return false;

            int slot = GetSlotForItemType(item.m_shared.m_itemType);
            if (slot < 0)
            {
                Plugin.Log.LogDebug($"Item type {item.m_shared.m_itemType} has no equipment slot, skipping");
                return false;
            }

            var playerInventory = player.GetInventory();
            if (playerInventory == null) return false;

            // Check if there's already an item in this slot
            var existingItem = _equipmentInventory.GetItemAt(slot, 0);
            if (existingItem != null)
            {
                // Move existing item back to player inventory first
                if (!MoveToInventory(player, existingItem))
                {
                    Plugin.Log.LogWarning($"Failed to move existing equipped item back to inventory");
                    return false;
                }
            }

            // Remove from player inventory (don't destroy the item data)
            playerInventory.RemoveItem(item);

            // Add to equipment storage at the correct slot position
            // Directly add to inventory list to preserve grid position
            item.m_gridPos = new Vector2i(slot, 0);
            AddItemDirect(_equipmentInventory, item);

            Plugin.Log.LogDebug($"Moved {item.m_shared.m_name} (type: {item.m_shared.m_itemType}) to equipment slot {slot}");
            return true;
        }

        /// <summary>
        /// Moves an item from equipment storage back to player inventory.
        /// Called before unequipping.
        /// </summary>
        public bool MoveToInventory(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null || _equipmentInventory == null) return false;

            var playerInventory = player.GetInventory();
            if (playerInventory == null) return false;

            // Find an empty slot in player inventory
            Vector2i emptySlot = FindEmptySlot(playerInventory);
            if (emptySlot.x < 0)
            {
                Plugin.Log.LogWarning("No empty inventory slot for unequipped item");
                return false;
            }

            // Remove from equipment storage
            _equipmentInventory.RemoveItem(item);

            // Add to player inventory
            item.m_gridPos = emptySlot;
            playerInventory.AddItem(item);

            Plugin.Log.LogDebug($"Moved {item.m_shared.m_name} back to inventory at ({emptySlot.x}, {emptySlot.y})");
            return true;
        }

        /// <summary>
        /// Gets the item in a specific equipment slot.
        /// </summary>
        public ItemDrop.ItemData GetEquippedItem(int slot)
        {
            return _equipmentInventory?.GetItemAt(slot, 0);
        }

        /// <summary>
        /// Gets all equipped items.
        /// </summary>
        public List<ItemDrop.ItemData> GetAllEquippedItems()
        {
            return _equipmentInventory?.GetAllItems() ?? new List<ItemDrop.ItemData>();
        }

        /// <summary>
        /// Saves equipment inventory to player's custom data.
        /// </summary>
        public void Save(Player player)
        {
            if (player == null || _equipmentInventory == null) return;

            try
            {
                var pkg = new ZPackage();
                _equipmentInventory.Save(pkg);
                string base64 = Convert.ToBase64String(pkg.GetArray());
                player.m_customData[SAVE_KEY] = base64;
                Plugin.Log.LogDebug($"Saved equipment inventory ({_equipmentInventory.GetAllItems().Count} items)");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save equipment inventory: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads equipment inventory from player's custom data.
        /// </summary>
        public void Load(Player player)
        {
            if (player == null || _equipmentInventory == null) return;

            try
            {
                if (player.m_customData.TryGetValue(SAVE_KEY, out string base64) && !string.IsNullOrEmpty(base64))
                {
                    byte[] data = Convert.FromBase64String(base64);
                    var pkg = new ZPackage(data);
                    _equipmentInventory.Load(pkg);
                    Plugin.Log.LogDebug($"Loaded equipment inventory ({_equipmentInventory.GetAllItems().Count} items)");

                    // Re-equip all items
                    ReequipAllItems(player);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load equipment inventory: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-equips all items from equipment storage after loading.
        /// </summary>
        private void ReequipAllItems(Player player)
        {
            if (player == null) return;

            foreach (var item in _equipmentInventory.GetAllItems())
            {
                // Use reflection or Humanoid method to equip without triggering our patches
                EquipItemDirect(player, item);
            }
        }

        /// <summary>
        /// Directly equips an item without moving it (used during load).
        /// </summary>
        private void EquipItemDirect(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null) return;

            // Set the appropriate equipment slot on Humanoid using reflection (fields are protected)
            string fieldName = item.m_shared.m_itemType switch
            {
                ItemDrop.ItemData.ItemType.Helmet => "m_helmetItem",
                ItemDrop.ItemData.ItemType.Chest => "m_chestItem",
                ItemDrop.ItemData.ItemType.Legs => "m_legItem",
                ItemDrop.ItemData.ItemType.Shoulder => "m_shoulderItem",
                ItemDrop.ItemData.ItemType.Utility => "m_utilityItem",
                ItemDrop.ItemData.ItemType.Shield => "m_leftItem",
                ItemDrop.ItemData.ItemType.OneHandedWeapon => "m_rightItem",
                ItemDrop.ItemData.ItemType.TwoHandedWeapon => "m_rightItem",
                ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft => "m_rightItem",
                ItemDrop.ItemData.ItemType.Bow => "m_rightItem",
                ItemDrop.ItemData.ItemType.Tool => "m_rightItem",
                ItemDrop.ItemData.ItemType.Torch => "m_rightItem",
                ItemDrop.ItemData.ItemType.Ammo => "m_ammoItem",
                _ => null
            };

            if (fieldName != null)
            {
                SetHumanoidEquipmentField(player, fieldName, item);
            }

            // Setup visual equipment
            player.SetupEquipment();
        }

        // Cache for Humanoid equipment fields
        private static readonly Dictionary<string, FieldInfo> _humanoidFields = new();

        private static void SetHumanoidEquipmentField(Humanoid humanoid, string fieldName, ItemDrop.ItemData item)
        {
            if (!_humanoidFields.TryGetValue(fieldName, out var field))
            {
                field = typeof(Humanoid).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _humanoidFields[fieldName] = field;
            }

            field?.SetValue(humanoid, item);
        }

        /// <summary>
        /// Finds an empty slot in the inventory.
        /// </summary>
        private static Vector2i FindEmptySlot(Inventory inventory)
        {
            int width = inventory.GetWidth();
            int height = inventory.GetHeight();

            // Search rows 1+ first (row 0 is often hotbar)
            for (int y = 1; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        return new Vector2i(x, y);
                    }
                }
            }

            // Then try row 0 (hotbar)
            for (int x = 0; x < width; x++)
            {
                if (inventory.GetItemAt(x, 0) == null)
                {
                    return new Vector2i(x, 0);
                }
            }

            return new Vector2i(-1, -1);
        }

        #region Reflection Helpers

        private static FieldInfo _inventoryListField;
        private static MethodInfo _changedMethod;

        /// <summary>
        /// Adds an item directly to the inventory's internal list, preserving grid position.
        /// </summary>
        private static void AddItemDirect(Inventory inventory, ItemDrop.ItemData item)
        {
            if (_inventoryListField == null)
            {
                _inventoryListField = typeof(Inventory).GetField("m_inventory", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (_changedMethod == null)
            {
                _changedMethod = typeof(Inventory).GetMethod("Changed", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            var list = _inventoryListField?.GetValue(inventory) as List<ItemDrop.ItemData>;
            if (list != null)
            {
                list.Add(item);
                _changedMethod?.Invoke(inventory, null);
            }
        }

        #endregion
    }
}
