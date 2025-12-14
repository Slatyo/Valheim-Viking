using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using Viking.Commands;
using Viking.Core;
using Viking.Data;
using Viking.Integration;
using Viking.Network;
using Viking.Talents;

namespace Viking
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("com.slatyo.state")]
    [BepInDependency("com.slatyo.vital")]
    [BepInDependency("com.slatyo.munin", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.slatyo.prime", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.slatyo.veneer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.slatyo.spark", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.slatyo.tome", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.slatyo.viking";
        public const string PluginName = "Viking";
        public const string PluginVersion = "1.0.0";

        public static ManualLogSource Log { get; private set; }
        public static Plugin Instance { get; private set; }

        private Harmony _harmony;

        // Track which optional mods are available
        public static bool HasPrime { get; private set; }
        public static bool HasVeneer { get; private set; }
        public static bool HasSpark { get; private set; }
        public static bool HasTome { get; private set; }
        public static bool HasMunin { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"{PluginName} v{PluginVersion} is loading...");

            // Check for optional dependencies
            HasMunin = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.slatyo.munin");
            HasPrime = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.slatyo.prime");
            HasVeneer = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.slatyo.veneer");
            HasSpark = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.slatyo.spark");
            HasTome = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.slatyo.tome");

            Log.LogInfo($"Optional dependencies: Prime={HasPrime}, Veneer={HasVeneer}, Spark={HasSpark}, Tome={HasTome}, Munin={HasMunin}");

            // Initialize talent tree definitions
            TalentTreeManager.Initialize();

            // Initialize player data storage (registers with State)
            VikingDataStore.Initialize();

            // Initialize network RPCs
            VikingNetwork.Initialize();

            // Initialize Harmony patches
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            // Initialize integrations
            if (HasPrime) PrimeIntegration.Initialize();
            if (HasVeneer) VeneerIntegration.Initialize();
            if (HasSpark) SparkIntegration.Initialize();
            if (HasTome) TomeIntegration.Initialize();

            // Register Munin commands if available
            if (HasMunin)
            {
                VikingCommands.Register();
            }

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded successfully");
        }

        private void Update()
        {
            // Check ability bar input
            AbilityBar.CheckInput();

            // Check UI toggle input
            if (HasVeneer)
            {
                VeneerIntegration.CheckInput();
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();

            // Cleanup integrations
            if (HasPrime) PrimeIntegration.Cleanup();
            if (HasVeneer) VeneerIntegration.Cleanup();
        }

        /// <summary>
        /// Check if we're the server/host.
        /// </summary>
        public static bool IsServer()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }
    }
}
