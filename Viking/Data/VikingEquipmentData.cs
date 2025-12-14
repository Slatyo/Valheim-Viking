using System;
using System.Collections.Generic;
using SimpleJson;
using State;

namespace Viking.Data
{
    /// <summary>
    /// Persistent equipment and inventory data for the Viking mod.
    /// Stored via State.Store and persists with world saves as JSON.
    /// This replaces vanilla inventory persistence to ensure server-authoritative storage.
    /// </summary>
    public class VikingEquipmentData : IPlayerData
    {
        /// <summary>
        /// Equipment slots (0-7: helmet, chest, legs, shoulder, utility, weapon_r, weapon_l, ammo).
        /// </summary>
        public Dictionary<int, SerializedItem> Equipment { get; set; } = new();

        /// <summary>
        /// Full player inventory including all items.
        /// </summary>
        public List<SerializedItem> Inventory { get; set; } = new();

        /// <summary>
        /// Serialized item data for persistence.
        /// </summary>
        public class SerializedItem
        {
            public string Prefab { get; set; } = "";
            public int Stack { get; set; } = 1;
            public float Durability { get; set; } = 100f;
            public int Quality { get; set; } = 1;
            public int Variant { get; set; } = 0;
            public long CrafterId { get; set; } = 0;
            public string CrafterName { get; set; } = "";
            public int GridX { get; set; } = 0;
            public int GridY { get; set; } = 0;
            public bool IsEquipped { get; set; } = false;
            public Dictionary<string, string> CustomData { get; set; } = new();

            public JsonObject ToJson()
            {
                var obj = new JsonObject
                {
                    ["prefab"] = Prefab,
                    ["stack"] = Stack,
                    ["durability"] = Durability,
                    ["quality"] = Quality,
                    ["variant"] = Variant,
                    ["crafterId"] = CrafterId,
                    ["crafterName"] = CrafterName,
                    ["gridX"] = GridX,
                    ["gridY"] = GridY,
                    ["equipped"] = IsEquipped
                };

                // Serialize custom data
                if (CustomData != null && CustomData.Count > 0)
                {
                    var customObj = new JsonObject();
                    foreach (var kvp in CustomData)
                    {
                        customObj[kvp.Key] = kvp.Value;
                    }
                    obj["custom"] = customObj;
                }

                return obj;
            }

            public static SerializedItem FromJson(JsonObject obj)
            {
                if (obj == null) return null;

                var item = new SerializedItem();

                if (obj.TryGetValue("prefab", out var prefab) && prefab != null)
                    item.Prefab = prefab.ToString();

                if (obj.TryGetValue("stack", out var stack) && stack != null)
                    item.Stack = Convert.ToInt32(stack);

                if (obj.TryGetValue("durability", out var durability) && durability != null)
                    item.Durability = Convert.ToSingle(durability);

                if (obj.TryGetValue("quality", out var quality) && quality != null)
                    item.Quality = Convert.ToInt32(quality);

                if (obj.TryGetValue("variant", out var variant) && variant != null)
                    item.Variant = Convert.ToInt32(variant);

                if (obj.TryGetValue("crafterId", out var crafterId) && crafterId != null)
                    item.CrafterId = Convert.ToInt64(crafterId);

                if (obj.TryGetValue("crafterName", out var crafterName) && crafterName != null)
                    item.CrafterName = crafterName.ToString();

                if (obj.TryGetValue("gridX", out var gridX) && gridX != null)
                    item.GridX = Convert.ToInt32(gridX);

                if (obj.TryGetValue("gridY", out var gridY) && gridY != null)
                    item.GridY = Convert.ToInt32(gridY);

                if (obj.TryGetValue("equipped", out var equipped) && equipped != null)
                    item.IsEquipped = Convert.ToBoolean(equipped);

                // Deserialize custom data
                if (obj.TryGetValue("custom", out var customVal) && customVal is JsonObject customObj)
                {
                    item.CustomData = new Dictionary<string, string>();
                    foreach (var kvp in customObj)
                    {
                        if (kvp.Value != null)
                        {
                            item.CustomData[kvp.Key] = kvp.Value.ToString();
                        }
                    }
                }

                return item;
            }
        }

        public void Initialize()
        {
            Equipment = new Dictionary<int, SerializedItem>();
            Inventory = new List<SerializedItem>();
        }

        public string Serialize()
        {
            var obj = new JsonObject();

            // Serialize equipment slots
            var equipObj = new JsonObject();
            foreach (var kvp in Equipment)
            {
                equipObj[kvp.Key.ToString()] = kvp.Value.ToJson();
            }
            obj["equipment"] = equipObj;

            // Serialize inventory
            var invArray = new JsonArray();
            foreach (var item in Inventory)
            {
                invArray.Add(item.ToJson());
            }
            obj["inventory"] = invArray;

            return SimpleJson.SimpleJson.SerializeObject(obj);
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            try
            {
                var obj = SimpleJson.SimpleJson.DeserializeObject<JsonObject>(data);
                if (obj == null) return;

                // Deserialize equipment slots
                Equipment.Clear();
                if (obj.TryGetValue("equipment", out var equipVal) && equipVal is JsonObject equipObj)
                {
                    foreach (var kvp in equipObj)
                    {
                        if (int.TryParse(kvp.Key, out int slot) && kvp.Value is JsonObject itemObj)
                        {
                            var item = SerializedItem.FromJson(itemObj);
                            if (item != null && !string.IsNullOrEmpty(item.Prefab))
                            {
                                Equipment[slot] = item;
                            }
                        }
                    }
                }

                // Deserialize inventory
                Inventory.Clear();
                if (obj.TryGetValue("inventory", out var invVal) && invVal is JsonArray invArray)
                {
                    foreach (var itemVal in invArray)
                    {
                        if (itemVal is JsonObject itemObj)
                        {
                            var item = SerializedItem.FromJson(itemObj);
                            if (item != null && !string.IsNullOrEmpty(item.Prefab))
                            {
                                Inventory.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to deserialize VikingEquipmentData: {ex.Message}");
            }
        }

        public bool Validate()
        {
            // Ensure collections exist
            if (Equipment == null) Equipment = new Dictionary<int, SerializedItem>();
            if (Inventory == null) Inventory = new List<SerializedItem>();

            // Remove items with invalid prefabs
            var invalidSlots = new List<int>();
            foreach (var kvp in Equipment)
            {
                if (kvp.Value == null || string.IsNullOrEmpty(kvp.Value.Prefab))
                {
                    invalidSlots.Add(kvp.Key);
                }
            }
            foreach (var slot in invalidSlots)
            {
                Equipment.Remove(slot);
            }

            // Remove invalid inventory items
            Inventory.RemoveAll(item => item == null || string.IsNullOrEmpty(item.Prefab));

            return true;
        }

        /// <summary>
        /// Clear all equipment and inventory data.
        /// </summary>
        public void Clear()
        {
            Equipment.Clear();
            Inventory.Clear();
        }

        /// <summary>
        /// Get item count in inventory (excluding equipment).
        /// </summary>
        public int GetInventoryCount()
        {
            return Inventory.Count;
        }

        /// <summary>
        /// Get equipped item count.
        /// </summary>
        public int GetEquippedCount()
        {
            return Equipment.Count;
        }
    }
}
