using Viking.Core;

namespace Viking.Integration
{
    /// <summary>
    /// Integration with Tome for currency and items.
    /// </summary>
    public static class TomeIntegration
    {
        /// <summary>Currency used for full talent reset.</summary>
        public const string RESET_CURRENCY = "SoulFragment";

        /// <summary>Cost for full talent reset.</summary>
        public const int RESET_COST = 50;

        /// <summary>Currency used for backtracking.</summary>
        public const string BACKTRACK_CURRENCY = "SoulFragment";

        /// <summary>Cost per backtrack point.</summary>
        public const int BACKTRACK_COST = 1;

        /// <summary>
        /// Initialize Tome integration.
        /// </summary>
        public static void Initialize()
        {
            Plugin.Log.LogInfo("Tome integration initialized");
        }

        /// <summary>
        /// Check if player can afford a full reset.
        /// </summary>
        public static bool CanAffordReset(Player player)
        {
            if (player == null) return false;

            // TODO: Use Tome API when available
            // return TomeAPI.HasItem(player, RESET_CURRENCY, RESET_COST);

            // For now, always allow
            return true;
        }

        /// <summary>
        /// Consume currency for a full reset.
        /// </summary>
        public static bool ConsumeResetCurrency(Player player)
        {
            if (player == null) return false;

            // TODO: Use Tome API when available
            // return TomeAPI.RemoveItem(player, RESET_CURRENCY, RESET_COST);

            // For now, always succeed
            Plugin.Log.LogDebug($"Would consume {RESET_COST} {RESET_CURRENCY} for reset");
            return true;
        }

        /// <summary>
        /// Check if player can afford a backtrack.
        /// </summary>
        public static bool CanAffordBacktrack(Player player)
        {
            if (player == null) return false;

            // TODO: Use Tome API when available
            // return TomeAPI.HasItem(player, BACKTRACK_CURRENCY, BACKTRACK_COST);

            // For now, always allow
            return true;
        }

        /// <summary>
        /// Consume currency for a backtrack.
        /// </summary>
        public static bool ConsumeBacktrackCurrency(Player player)
        {
            if (player == null) return false;

            // TODO: Use Tome API when available
            // return TomeAPI.RemoveItem(player, BACKTRACK_CURRENCY, BACKTRACK_COST);

            // For now, always succeed
            Plugin.Log.LogDebug($"Would consume {BACKTRACK_COST} {BACKTRACK_CURRENCY} for backtrack");
            return true;
        }

        /// <summary>
        /// Get the player's current reset currency amount.
        /// </summary>
        public static int GetResetCurrencyAmount(Player player)
        {
            if (player == null) return 0;

            // TODO: Use Tome API when available
            // return TomeAPI.GetItemCount(player, RESET_CURRENCY);

            return 0;
        }

        /// <summary>
        /// Get the player's current backtrack currency amount.
        /// </summary>
        public static int GetBacktrackCurrencyAmount(Player player)
        {
            if (player == null) return 0;

            // TODO: Use Tome API when available
            // return TomeAPI.GetItemCount(player, BACKTRACK_CURRENCY);

            return 0;
        }
    }
}
