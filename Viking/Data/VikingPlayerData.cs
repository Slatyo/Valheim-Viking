using System;
using System.Collections.Generic;
using System.Linq;
using SimpleJson;
using Vital.Data;

namespace Viking.Data
{
    /// <summary>
    /// Persistent player data for the Viking talent system.
    /// Stored via VitalDataStore and persists with world saves as JSON.
    /// </summary>
    public class VikingPlayerData : IPlayerData
    {
        /// <summary>Starting point in the talent tree (Warrior, Archer, Mage, Healer).</summary>
        public string StartingPoint { get; set; } = "";

        /// <summary>Allocated talent nodes with their rank counts.</summary>
        public Dictionary<string, int> AllocatedNodes { get; set; } = new();

        /// <summary>Allocation history for backtracking (most recent last).</summary>
        public List<string> AllocationHistory { get; set; } = new();

        /// <summary>Ability bar slot assignments (slot index -> ability ID).</summary>
        public Dictionary<int, string> AbilitySlots { get; set; } = new();

        /// <summary>Last respec timestamp (UTC ticks).</summary>
        public long LastRespecTime { get; set; } = 0;

        /// <summary>Total points spent in talents.</summary>
        public int SpentPoints => AllocatedNodes.Values.Sum();

        public void Initialize()
        {
            StartingPoint = "";
            AllocatedNodes = new Dictionary<string, int>();
            AllocationHistory = new List<string>();
            AbilitySlots = new Dictionary<int, string>();
            LastRespecTime = 0;
        }

        public string Serialize()
        {
            var obj = new JsonObject
            {
                ["start"] = StartingPoint,
                ["lastRespec"] = LastRespecTime
            };

            // Serialize allocated nodes
            var nodesObj = new JsonObject();
            foreach (var kvp in AllocatedNodes)
            {
                nodesObj[kvp.Key] = kvp.Value;
            }
            obj["nodes"] = nodesObj;

            // Serialize allocation history
            var historyArray = new JsonArray();
            foreach (var nodeId in AllocationHistory)
            {
                historyArray.Add(nodeId);
            }
            obj["history"] = historyArray;

            // Serialize ability slots
            var slotsObj = new JsonObject();
            foreach (var kvp in AbilitySlots)
            {
                slotsObj[kvp.Key.ToString()] = kvp.Value;
            }
            obj["slots"] = slotsObj;

            return SimpleJson.SimpleJson.SerializeObject(obj);
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            try
            {
                var obj = SimpleJson.SimpleJson.DeserializeObject<JsonObject>(data);
                if (obj == null) return;

                // Starting point
                if (obj.TryGetValue("start", out var startVal) && startVal != null)
                {
                    StartingPoint = startVal.ToString();
                }

                // Last respec time
                if (obj.TryGetValue("lastRespec", out var respecVal) && respecVal != null)
                {
                    LastRespecTime = Convert.ToInt64(respecVal);
                }

                // Allocated nodes
                AllocatedNodes.Clear();
                if (obj.TryGetValue("nodes", out var nodesVal) && nodesVal is JsonObject nodesObj)
                {
                    foreach (var kvp in nodesObj)
                    {
                        if (kvp.Value != null)
                        {
                            AllocatedNodes[kvp.Key] = Convert.ToInt32(kvp.Value);
                        }
                    }
                }

                // Allocation history
                AllocationHistory.Clear();
                if (obj.TryGetValue("history", out var historyVal) && historyVal is JsonArray historyArray)
                {
                    foreach (var item in historyArray)
                    {
                        if (item != null)
                        {
                            AllocationHistory.Add(item.ToString());
                        }
                    }
                }

                // Ability slots
                AbilitySlots.Clear();
                if (obj.TryGetValue("slots", out var slotsVal) && slotsVal is JsonObject slotsObj)
                {
                    foreach (var kvp in slotsObj)
                    {
                        if (int.TryParse(kvp.Key, out int slot) && kvp.Value != null)
                        {
                            AbilitySlots[slot] = kvp.Value.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to deserialize VikingPlayerData: {ex.Message}");
            }
        }

        public bool Validate()
        {
            // Basic validation
            if (AllocatedNodes == null) AllocatedNodes = new Dictionary<string, int>();
            if (AllocationHistory == null) AllocationHistory = new List<string>();
            if (AbilitySlots == null) AbilitySlots = new Dictionary<int, string>();

            // Remove invalid node ranks
            var invalidNodes = AllocatedNodes.Where(kvp => kvp.Value <= 0).Select(kvp => kvp.Key).ToList();
            foreach (var nodeId in invalidNodes)
            {
                AllocatedNodes.Remove(nodeId);
            }

            return true;
        }

        /// <summary>
        /// Allocate a point to a node.
        /// </summary>
        public void AllocateNode(string nodeId)
        {
            if (!AllocatedNodes.ContainsKey(nodeId))
            {
                AllocatedNodes[nodeId] = 0;
            }
            AllocatedNodes[nodeId]++;
            AllocationHistory.Add(nodeId);
        }

        /// <summary>
        /// Deallocate the most recent point (backtrack).
        /// </summary>
        /// <returns>The node ID that was deallocated, or null if nothing to undo.</returns>
        public string DeallocateLastNode()
        {
            if (AllocationHistory.Count == 0) return null;

            string lastNodeId = AllocationHistory[AllocationHistory.Count - 1];
            AllocationHistory.RemoveAt(AllocationHistory.Count - 1);

            if (AllocatedNodes.TryGetValue(lastNodeId, out int ranks))
            {
                if (ranks > 1)
                {
                    AllocatedNodes[lastNodeId] = ranks - 1;
                }
                else
                {
                    AllocatedNodes.Remove(lastNodeId);
                }
            }

            return lastNodeId;
        }

        /// <summary>
        /// Reset all talent allocations.
        /// </summary>
        public void ResetAllNodes()
        {
            AllocatedNodes.Clear();
            AllocationHistory.Clear();
            LastRespecTime = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Get rank count for a specific node.
        /// </summary>
        public int GetNodeRanks(string nodeId)
        {
            return AllocatedNodes.TryGetValue(nodeId, out int ranks) ? ranks : 0;
        }

        /// <summary>
        /// Check if a node has any points allocated.
        /// </summary>
        public bool HasNode(string nodeId)
        {
            return AllocatedNodes.ContainsKey(nodeId) && AllocatedNodes[nodeId] > 0;
        }
    }
}
