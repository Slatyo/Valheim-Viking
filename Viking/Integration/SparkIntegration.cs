using UnityEngine;
using Viking.Core;

namespace Viking.Integration
{
    /// <summary>
    /// Integration with Spark for VFX and audio.
    /// </summary>
    public static class SparkIntegration
    {
        /// <summary>
        /// Initialize Spark integration.
        /// </summary>
        public static void Initialize()
        {
            // Subscribe to Viking events for VFX
            VikingServer.OnNodeAllocated += OnNodeAllocated;
            VikingServer.OnTalentsReset += OnTalentsReset;

            // Subscribe to Vital level changed events
            Vital.Core.Leveling.OnLevelChanged += OnLevelChanged;

            Plugin.Log.LogInfo("Spark integration initialized");
        }

        /// <summary>
        /// Called when a talent node is allocated - play VFX.
        /// </summary>
        private static void OnNodeAllocated(long playerId, string nodeId, int newRank)
        {
            var player = GetPlayerByID(playerId);
            if (player == null) return;

            // Play allocation VFX at player position
            PlayNodeAllocationEffect(player.transform.position);
        }

        /// <summary>
        /// Called when talents are reset - play VFX.
        /// </summary>
        private static void OnTalentsReset(long playerId)
        {
            var player = GetPlayerByID(playerId);
            if (player == null) return;

            // Play reset VFX at player position
            PlayResetEffect(player.transform.position);
        }

        /// <summary>
        /// Called when a player's level changes - play VFX if it went up.
        /// </summary>
        private static void OnLevelChanged(Character character, int oldLevel, int newLevel)
        {
            if (character is not Player player) return;
            if (newLevel <= oldLevel) return; // Only play on level up

            // Play level up VFX
            PlayLevelUpEffect(player.transform.position, newLevel);
        }

        /// <summary>
        /// Play node allocation effect.
        /// </summary>
        private static void PlayNodeAllocationEffect(Vector3 position)
        {
            // TODO: Use Spark API when available
            // Spark.Effects.Play("talent_allocate", position);

            Plugin.Log.LogDebug($"Would play node allocation VFX at {position}");
        }

        /// <summary>
        /// Play talent reset effect.
        /// </summary>
        private static void PlayResetEffect(Vector3 position)
        {
            // TODO: Use Spark API when available
            // Spark.Effects.Play("talent_reset", position);

            Plugin.Log.LogDebug($"Would play reset VFX at {position}");
        }

        /// <summary>
        /// Play level up effect.
        /// </summary>
        private static void PlayLevelUpEffect(Vector3 position, int level)
        {
            // TODO: Use Spark API when available
            // Spark.Effects.Play("level_up", position);
            // Spark.Audio.Play("level_up_sound", position);

            Plugin.Log.LogDebug($"Would play level up VFX at {position} for level {level}");
        }

        /// <summary>
        /// Get a Player instance by player ID.
        /// </summary>
        private static Player GetPlayerByID(long playerId)
        {
            foreach (var player in Player.GetAllPlayers())
            {
                if (player.GetPlayerID() == playerId)
                {
                    return player;
                }
            }
            return null;
        }
    }
}
