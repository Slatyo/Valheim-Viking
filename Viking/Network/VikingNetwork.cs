using System;
using Viking.Core;
using Viking.Data;

namespace Viking.Network
{
    /// <summary>
    /// Network RPCs for Viking talent system.
    /// Client sends requests, server validates and applies.
    /// </summary>
    public static class VikingNetwork
    {
        private const string RPC_REQUEST_START = "Viking_RequestStart";
        private const string RPC_REQUEST_ALLOCATE = "Viking_RequestAllocate";
        private const string RPC_REQUEST_BACKTRACK = "Viking_RequestBacktrack";
        private const string RPC_REQUEST_RESET = "Viking_RequestReset";
        private const string RPC_REQUEST_SET_SLOT = "Viking_RequestSetSlot";

        /// <summary>
        /// Initialize network RPCs.
        /// </summary>
        internal static void Initialize()
        {
            // Register RPCs when ZRoutedRpc is available
            if (ZRoutedRpc.instance != null)
            {
                RegisterRPCs();
            }
            else
            {
                // Wait for ZRoutedRpc to be ready
                Jotunn.Managers.PrefabManager.OnPrefabsRegistered += OnPrefabsRegistered;
            }
        }

        private static void OnPrefabsRegistered()
        {
            RegisterRPCs();
            Jotunn.Managers.PrefabManager.OnPrefabsRegistered -= OnPrefabsRegistered;
        }

        private static void RegisterRPCs()
        {
            if (ZRoutedRpc.instance == null) return;

            ZRoutedRpc.instance.Register<string>(RPC_REQUEST_START, RPC_OnRequestStart);
            ZRoutedRpc.instance.Register<string>(RPC_REQUEST_ALLOCATE, RPC_OnRequestAllocate);
            ZRoutedRpc.instance.Register(RPC_REQUEST_BACKTRACK, RPC_OnRequestBacktrack);
            ZRoutedRpc.instance.Register(RPC_REQUEST_RESET, RPC_OnRequestReset);
            ZRoutedRpc.instance.Register<int, string>(RPC_REQUEST_SET_SLOT, RPC_OnRequestSetSlot);

            Plugin.Log.LogInfo("Viking network RPCs registered");
        }

        #region Client Requests

        /// <summary>
        /// Request to choose a starting point.
        /// </summary>
        public static void RequestChooseStart(string startingPointId)
        {
            if (Plugin.IsServer())
            {
                // Server can call directly
                long playerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
                VikingServer.ChooseStartingPoint(playerId, startingPointId);
            }
            else
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_REQUEST_START, startingPointId);
            }
        }

        /// <summary>
        /// Request to allocate a talent node.
        /// </summary>
        public static void RequestAllocateNode(string nodeId)
        {
            if (Plugin.IsServer())
            {
                long playerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
                VikingServer.AllocateNode(playerId, nodeId);
            }
            else
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_REQUEST_ALLOCATE, nodeId);
            }
        }

        /// <summary>
        /// Request to backtrack (undo last allocation).
        /// </summary>
        public static void RequestBacktrack()
        {
            if (Plugin.IsServer())
            {
                long playerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
                VikingServer.Backtrack(playerId);
            }
            else
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_REQUEST_BACKTRACK);
            }
        }

        /// <summary>
        /// Request to reset all talents.
        /// </summary>
        public static void RequestReset()
        {
            if (Plugin.IsServer())
            {
                long playerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
                VikingServer.FullReset(playerId);
            }
            else
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_REQUEST_RESET);
            }
        }

        /// <summary>
        /// Request to set an ability slot.
        /// </summary>
        public static void RequestSetAbilitySlot(int slot, string abilityId)
        {
            if (Plugin.IsServer())
            {
                long playerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
                VikingServer.SetAbilitySlot(playerId, slot, abilityId);
            }
            else
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_REQUEST_SET_SLOT, slot, abilityId ?? "");
            }
        }

        #endregion

        #region Server RPC Handlers

        private static void RPC_OnRequestStart(long sender, string startingPointId)
        {
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"RPC_OnRequestStart from {sender}: {startingPointId}");
            VikingServer.ChooseStartingPoint(sender, startingPointId);
        }

        private static void RPC_OnRequestAllocate(long sender, string nodeId)
        {
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"RPC_OnRequestAllocate from {sender}: {nodeId}");
            VikingServer.AllocateNode(sender, nodeId);
        }

        private static void RPC_OnRequestBacktrack(long sender)
        {
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"RPC_OnRequestBacktrack from {sender}");
            VikingServer.Backtrack(sender);
        }

        private static void RPC_OnRequestReset(long sender)
        {
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"RPC_OnRequestReset from {sender}");
            VikingServer.FullReset(sender);
        }

        private static void RPC_OnRequestSetSlot(long sender, int slot, string abilityId)
        {
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"RPC_OnRequestSetSlot from {sender}: slot={slot}, ability={abilityId}");
            VikingServer.SetAbilitySlot(sender, slot, string.IsNullOrEmpty(abilityId) ? null : abilityId);
        }

        #endregion
    }
}
