using System;
using System.Reflection;
using UnityEngine;

namespace Viking.Core
{
    /// <summary>
    /// Manages a dedicated inventory for equipped items.
    /// When items are equipped, they move from the main bag to this inventory.
    /// When unequipped, they move back to the bag.
    /// </summary>
    public class EquipmentInventory : MonoBehaviour
    {
        private static EquipmentInventory _instance;
        public static EquipmentInventory Instance => _instance;

        // Equipment slot indices (same mapping as EquipmentStorage)
        public const int SLOT_HELMET = 0;
        public const int SLOT_CHEST = 1;
        public const int SLOT_LEGS = 2;
        public const int SLOT_SHOULDER = 3;  // Cape
        public const int SLOT_UTILITY = 4;
        public const int SLOT_WEAPON_RIGHT = 5;
        public const int SLOT_WEAPON_LEFT = 6;  // Shield or second weapon
        public const int SLOT_AMMO = 7;

        private const int SLOT_COUNT = 8;

        // Reflection for private Inventory.AddItem(item, amount, x, y)
        private static readonly MethodInfo _addItemAtPositionMethod = typeof(Inventory).GetMethod(
            "AddItem",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) },
            null);

        // Real Valheim Inventory for equipment (8 slots, 1 row)
        private Inventory _inventory;
        public Inventory Inventory => _inventory;

        private Player _player;

        // Flag to prevent recursive equip/unequip handling
        private bool _isProcessing = false;

        private void Awake()
        {
            _instance = this;
            _player = GetComponent<Player>();

            // Create 8-slot inventory for equipment
            _inventory = new Inventory("Equipment", null, SLOT_COUNT, 1);

            // Log reflection method status
            if (_addItemAtPositionMethod != null)
            {
                Plugin.Log.LogDebug("EquipmentInventory: Reflection method found for AddItem(item, amount, x, y)");
            }
            else
            {
                Plugin.Log.LogWarning("EquipmentInventory: Could NOT find AddItem reflection method - slot positioning may fail!");
            }

            Plugin.Log.LogDebug("EquipmentInventory created");
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Creates the EquipmentInventory component on the player.
        /// </summary>
        public static void Create(Player player)
        {
            if (player == null) return;
            if (player.GetComponent<EquipmentInventory>() != null) return;

            player.gameObject.AddComponent<EquipmentInventory>();
            Plugin.Log.LogDebug($"EquipmentInventory created for {player.GetPlayerName()}");
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
        /// Gets the Humanoid field name for a slot.
        /// </summary>
        public static string GetFieldNameForSlot(int slot)
        {
            return slot switch
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
        }

        /// <summary>
        /// Called after vanilla EquipItem - moves item from bag to equipment inventory.
        /// </summary>
        public void OnItemEquipped(ItemDrop.ItemData item)
        {
            if (item == null) return;
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;

                int slot = GetSlotForItemType(item.m_shared.m_itemType);
                if (slot < 0)
                {
                    Plugin.Log.LogDebug($"No equipment slot for item type: {item.m_shared.m_itemType}");
                    return;
                }

                // Check if item is already in equipment inventory
                if (_inventory.ContainsItem(item))
                {
                    Plugin.Log.LogDebug($"Item {item.m_shared.m_name} already in equipment inventory");
                    return;
                }

                // Remove from player's main inventory (bag)
                var playerInv = _player.GetInventory();
                if (playerInv.ContainsItem(item))
                {
                    playerInv.RemoveItem(item);
                    Plugin.Log.LogDebug($"Removed {item.m_shared.m_name} from bag");
                }

                // Add to equipment inventory at the correct slot using reflection
                if (AddItemAtPosition(_inventory, item, slot, 0))
                {
                    Plugin.Log.LogDebug($"Moved {item.m_shared.m_name} to equipment slot {slot}");
                }
                else
                {
                    Plugin.Log.LogWarning($"Failed to add {item.m_shared.m_name} to equipment slot {slot}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in OnItemEquipped: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Called before vanilla UnequipItem - moves item from equipment inventory to bag.
        /// </summary>
        public void OnItemUnequipping(ItemDrop.ItemData item)
        {
            if (item == null) return;
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;

                int slot = GetSlotForItemType(item.m_shared.m_itemType);
                if (slot < 0)
                {
                    Plugin.Log.LogDebug($"No equipment slot for item type: {item.m_shared.m_itemType}");
                    return;
                }

                // Check if item is in equipment inventory
                if (!_inventory.ContainsItem(item))
                {
                    Plugin.Log.LogDebug($"Item {item.m_shared.m_name} not in equipment inventory");
                    return;
                }

                // Remove from equipment inventory
                _inventory.RemoveItem(item);

                // Find empty slot in player's main inventory
                var playerInv = _player.GetInventory();
                var emptySlot = FindEmptySlot(playerInv);

                if (emptySlot.x >= 0)
                {
                    // Set grid position first, then add (AddItem uses m_gridPos)
                    item.m_gridPos = emptySlot;
                    if (playerInv.AddItem(item))
                    {
                        Plugin.Log.LogDebug($"Moved {item.m_shared.m_name} to bag at ({emptySlot.x}, {emptySlot.y})");
                    }
                    else
                    {
                        // Fallback: drop on ground if add fails
                        ItemDrop.DropItem(item, item.m_stack, _player.transform.position, Quaternion.identity);
                        Plugin.Log.LogWarning($"Failed to add to bag - dropped {item.m_shared.m_name}");
                    }
                }
                else
                {
                    // No space - drop on ground
                    ItemDrop.DropItem(item, item.m_stack, _player.transform.position, Quaternion.identity);
                    Plugin.Log.LogWarning($"No inventory space - dropped {item.m_shared.m_name}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in OnItemUnequipping: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Adds an item to an inventory at a specific position using reflection.
        /// </summary>
        private bool AddItemAtPosition(Inventory inventory, ItemDrop.ItemData item, int x, int y)
        {
            if (_addItemAtPositionMethod == null)
            {
                Plugin.Log.LogError("Could not find Inventory.AddItem(item, amount, x, y) method");
                return false;
            }

            try
            {
                return (bool)_addItemAtPositionMethod.Invoke(inventory, new object[] { item, item.m_stack, x, y });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to invoke AddItem via reflection: {ex.Message}");
                return false;
            }
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

        /// <summary>
        /// Gets item in equipment slot.
        /// </summary>
        public ItemDrop.ItemData GetItemInSlot(int slot)
        {
            if (slot < 0 || slot >= SLOT_COUNT) return null;
            return _inventory.GetItemAt(slot, 0);
        }

        /// <summary>
        /// Gets all equipped items.
        /// </summary>
        public System.Collections.Generic.List<ItemDrop.ItemData> GetAllItems()
        {
            return _inventory.GetAllItems();
        }

        /// <summary>
        /// Clears all equipment (for reset).
        /// </summary>
        public void Clear()
        {
            _inventory.RemoveAll();
            Plugin.Log.LogDebug("EquipmentInventory cleared");
        }

        /// <summary>
        /// Adds an item directly to equipment inventory (for loading).
        /// Does not trigger equip logic.
        /// </summary>
        public bool AddItemDirect(ItemDrop.ItemData item, int slot)
        {
            if (item == null) return false;
            if (slot < 0 || slot >= SLOT_COUNT) return false;

            // Use position-aware AddItem via reflection to ensure item goes to exact slot
            return AddItemAtPosition(_inventory, item, slot, 0);
        }

        /// <summary>
        /// Removes an item directly from equipment inventory (for saving/clearing).
        /// Does not trigger unequip logic.
        /// </summary>
        public bool RemoveItemDirect(ItemDrop.ItemData item)
        {
            if (item == null) return false;
            return _inventory.RemoveItem(item);
        }

        /// <summary>
        /// Gets the count of equipped items.
        /// </summary>
        public int GetEquippedCount()
        {
            return _inventory.GetAllItems().Count;
        }

        /// <summary>
        /// Check if processing to prevent recursion.
        /// </summary>
        public bool IsProcessing => _isProcessing;

        /// <summary>
        /// Sets processing flag (for external control during load).
        /// </summary>
        public void SetProcessing(bool processing)
        {
            _isProcessing = processing;
        }
    }
}
