using System.Text;
using Munin;
using Viking.Core;
using Viking.Data;
using Viking.Integration;
using Viking.Talents;

namespace Viking.Commands
{
    /// <summary>
    /// Console commands for Viking talent system.
    /// </summary>
    internal static class VikingCommands
    {
        /// <summary>
        /// Register all Viking commands with Munin.
        /// </summary>
        internal static void Register()
        {
            // UI commands
            Command.Register("viking", new CommandConfig
            {
                Name = "tree",
                Description = "Open the talent tree UI",
                Handler = CmdTree
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "character",
                Description = "Open the character window",
                Handler = CmdCharacter
            });

            // Status commands
            Command.Register("viking", new CommandConfig
            {
                Name = "status",
                Description = "Show your talent status",
                Handler = CmdStatus
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "points",
                Description = "Show available and spent points",
                Handler = CmdPoints
            });

            // Starting point commands
            Command.Register("viking", new CommandConfig
            {
                Name = "starts",
                Description = "List available starting points",
                Handler = CmdListStarts
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "start",
                Description = "Choose a starting point",
                Usage = "<warrior|archer|mage|healer>",
                Examples = new[] { "warrior", "mage" },
                Handler = CmdChooseStart
            });

            // Talent commands
            Command.Register("viking", new CommandConfig
            {
                Name = "allocate",
                Description = "Allocate a point to a node",
                Usage = "<nodeId>",
                Examples = new[] { "str_1", "dex_2", "berserker" },
                Handler = CmdAllocate
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "nodes",
                Description = "List available nodes to allocate",
                Handler = CmdListNodes
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "allocated",
                Description = "List your allocated nodes",
                Handler = CmdListAllocated
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "node",
                Description = "Show details about a node",
                Usage = "<nodeId>",
                Examples = new[] { "str_1", "berserker" },
                Handler = CmdNodeInfo
            });

            // Respec commands
            Command.Register("viking", new CommandConfig
            {
                Name = "undo",
                Description = "Undo last talent allocation",
                Handler = CmdUndo
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "reset",
                Description = "Reset all talents",
                Handler = CmdReset
            });

            // Admin commands
            Command.Register("viking", new CommandConfig
            {
                Name = "freereset",
                Description = "Free reset (admin only)",
                Permission = PermissionLevel.Admin,
                Handler = CmdFreeReset
            });

            Command.Register("viking", new CommandConfig
            {
                Name = "allnodes",
                Description = "List all nodes in the tree",
                Handler = CmdAllNodes
            });

            Plugin.Log.LogInfo("Viking commands registered with Munin");
        }

        #region UI Commands

        private static CommandResult CmdTree(CommandArgs args)
        {
            if (!Plugin.HasVeneer)
                return CommandResult.Error("Veneer is not installed - UI not available");

            VeneerIntegration.OpenTalentTree();
            return CommandResult.Success("Opened talent tree");
        }

        private static CommandResult CmdCharacter(CommandArgs args)
        {
            if (!Plugin.HasVeneer)
                return CommandResult.Error("Veneer is not installed - UI not available");

            VeneerIntegration.ToggleCharacterWindow();
            return CommandResult.Success("Toggled character window");
        }

        #endregion

        #region Status Commands

        private static CommandResult CmdStatus(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            var data = VikingDataStore.Get(player);
            if (data == null)
                return CommandResult.Error("No Viking data found");

            int level = Core.Viking.GetLevel(player);
            int available = VikingDataStore.GetAvailablePoints(player);
            int spent = VikingDataStore.GetSpentPoints(player);
            string start = data.StartingPoint;

            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>Viking Status</color>");
            sb.AppendLine($"Level: {level}");
            sb.AppendLine($"Starting Point: {(string.IsNullOrEmpty(start) ? "<not chosen>" : start)}");
            sb.AppendLine($"Points: {available} available, {spent} spent");
            sb.AppendLine($"Allocated Nodes: {data.AllocatedNodes.Count}");

            return CommandResult.Info(sb.ToString().TrimEnd());
        }

        private static CommandResult CmdPoints(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            int level = Core.Viking.GetLevel(player);
            int available = VikingDataStore.GetAvailablePoints(player);
            int spent = VikingDataStore.GetSpentPoints(player);

            return CommandResult.Info($"Level {level}: {available} available, {spent} spent ({level} total)");
        }

        #endregion

        #region Starting Point Commands

        private static CommandResult CmdListStarts(CommandArgs args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>Starting Points</color>");

            foreach (var sp in TalentTreeManager.GetAllStartingPoints())
            {
                sb.AppendLine($"  {sp.Id} - {sp.Name}");
            }

            return CommandResult.Info(sb.ToString().TrimEnd());
        }

        private static CommandResult CmdChooseStart(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            string startId = args.Get<string>(0, "");
            if (string.IsNullOrEmpty(startId))
                return CommandResult.Error("Usage: munin viking start <warrior|archer|mage|healer>");

            var data = VikingDataStore.Get(player);
            if (data != null && !string.IsNullOrEmpty(data.StartingPoint))
                return CommandResult.Error($"Already chose starting point: {data.StartingPoint}");

            var startPoint = TalentTreeManager.GetStartingPoint(startId.ToLower());
            if (startPoint == null)
                return CommandResult.Error($"Invalid starting point: {startId}");

            Core.Viking.ChooseStartingPoint(startId.ToLower());
            return CommandResult.Success($"Chose starting point: {startId}");
        }

        #endregion

        #region Talent Commands

        private static CommandResult CmdAllocate(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            string nodeId = args.Get<string>(0, "");
            if (string.IsNullOrEmpty(nodeId))
                return CommandResult.Error("Usage: munin viking allocate <nodeId>");

            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null)
                return CommandResult.Error($"Node not found: {nodeId}");

            if (!Core.Viking.CanAllocate(player, nodeId))
            {
                var data = VikingDataStore.Get(player);
                if (string.IsNullOrEmpty(data?.StartingPoint))
                    return CommandResult.Error("Choose a starting point first: munin viking start <type>");
                if (VikingDataStore.GetAvailablePoints(player) <= 0)
                    return CommandResult.Error("No available points");

                return CommandResult.Error($"Cannot allocate to {nodeId} - not connected or maxed");
            }

            Core.Viking.RequestAllocateNode(nodeId);
            int newRanks = Core.Viking.GetNodeRanks(player, nodeId) + 1;
            return CommandResult.Success($"Allocated {nodeId} (rank {newRanks}/{node.MaxRanks})");
        }

        private static CommandResult CmdListNodes(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            var data = VikingDataStore.Get(player);
            if (data == null || string.IsNullOrEmpty(data.StartingPoint))
                return CommandResult.Error("Choose a starting point first");

            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>Available Nodes</color>");

            int count = 0;
            foreach (var node in TalentTreeManager.GetAllNodes())
            {
                if (node.Type == TalentNodeType.Start) continue;

                if (Core.Viking.CanAllocate(player, node.Id))
                {
                    int current = Core.Viking.GetNodeRanks(player, node.Id);
                    sb.AppendLine($"  {node.Id} ({current}/{node.MaxRanks}) - {node.Description}");
                    count++;
                }
            }

            if (count == 0)
            {
                sb.AppendLine("  <none available>");
            }

            return CommandResult.Info(sb.ToString().TrimEnd());
        }

        private static CommandResult CmdListAllocated(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            var allocated = Core.Viking.GetAllocatedNodes(player);
            if (allocated.Count == 0)
                return CommandResult.Info("No nodes allocated");

            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>Allocated Nodes</color>");

            foreach (var kvp in allocated)
            {
                var node = TalentTreeManager.GetNode(kvp.Key);
                string name = node?.Name ?? kvp.Key;
                sb.AppendLine($"  {kvp.Key} x{kvp.Value} - {name}");
            }

            return CommandResult.Info(sb.ToString().TrimEnd());
        }

        private static CommandResult CmdNodeInfo(CommandArgs args)
        {
            string nodeId = args.Get<string>(0, "");
            if (string.IsNullOrEmpty(nodeId))
                return CommandResult.Error("Usage: munin viking node <nodeId>");

            var node = TalentTreeManager.GetNode(nodeId);
            if (node == null)
                return CommandResult.Error($"Node not found: {nodeId}");

            var sb = new StringBuilder();
            sb.AppendLine($"<color=#FFD700>{node.Name}</color>");
            sb.AppendLine($"ID: {node.Id}");
            sb.AppendLine($"Type: {node.Type}");
            sb.AppendLine($"Max Ranks: {node.MaxRanks}");
            sb.AppendLine($"Description: {node.Description}");

            if (node.Modifiers.Count > 0)
            {
                sb.AppendLine("Modifiers (per rank):");
                foreach (var mod in node.Modifiers)
                {
                    string sign = mod.Value >= 0 ? "+" : "";
                    string suffix = mod.Type == TalentModifierType.Percent ? "%" : "";
                    sb.AppendLine($"  {sign}{mod.Value}{suffix} {mod.Stat}");
                }
            }

            if (node.HasAbility)
            {
                sb.AppendLine($"Grants Ability: {node.GrantsAbility}");
            }

            if (node.Connections.Count > 0)
            {
                sb.AppendLine($"Connections: {string.Join(", ", node.Connections)}");
            }

            return CommandResult.Info(sb.ToString().TrimEnd());
        }

        private static CommandResult CmdAllNodes(CommandArgs args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFD700>All Talent Nodes</color>");

            foreach (var type in new[] { TalentNodeType.Start, TalentNodeType.Minor, TalentNodeType.Notable, TalentNodeType.Keystone })
            {
                var nodes = TalentTreeManager.GetNodesByType(type);
                sb.AppendLine($"\n<color=#AAAAAA>[{type}]</color>");
                foreach (var node in nodes)
                {
                    sb.AppendLine($"  {node.Id} - {node.Description}");
                }
            }

            return CommandResult.Info(sb.ToString().TrimEnd());
        }

        #endregion

        #region Respec Commands

        private static CommandResult CmdUndo(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            if (!Core.Viking.CanBacktrack(player))
                return CommandResult.Error("Nothing to undo");

            Core.Viking.RequestBacktrack();
            return CommandResult.Success("Undid last allocation");
        }

        private static CommandResult CmdReset(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            // TODO: Check for currency
            Core.Viking.RequestFullReset();
            return CommandResult.Success("Reset all talents");
        }

        private static CommandResult CmdFreeReset(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            // Admin can do free reset directly on server
            if (Plugin.IsServer())
            {
                VikingServer.FullReset(player.GetPlayerID(), free: true);
                return CommandResult.Success("Admin reset all talents (free)");
            }

            return CommandResult.Error("Admin reset only works on server");
        }

        #endregion
    }
}
