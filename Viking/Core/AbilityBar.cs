using System.Collections.Generic;
using UnityEngine;
using Viking.Data;
using Viking.Network;

namespace Viking.Core
{
    /// <summary>
    /// Manages the ability bar for players.
    /// Integrates with Prime for ability casting.
    /// </summary>
    public static class AbilityBar
    {
        /// <summary>Number of ability slots.</summary>
        public const int SlotCount = 8;

        /// <summary>Default keybinds for ability slots.</summary>
        public static readonly KeyCode[] DefaultKeybinds = new[]
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8
        };

        /// <summary>Event raised when an ability is used (for UI feedback).</summary>
        public static event System.Action<int, string, bool> OnAbilityUsed;

        /// <summary>
        /// Get ability in a specific slot.
        /// </summary>
        public static string GetSlot(Player player, int slot)
        {
            if (player == null || slot < 0 || slot >= SlotCount) return null;

            var data = VikingDataStore.Get(player);
            if (data == null) return null;

            return data.AbilitySlots.TryGetValue(slot, out string abilityId) ? abilityId : null;
        }

        /// <summary>
        /// Set ability in a specific slot (sends request to server).
        /// </summary>
        public static void SetSlot(int slot, string abilityId)
        {
            if (slot < 0 || slot >= SlotCount) return;
            VikingNetwork.RequestSetAbilitySlot(slot, abilityId);
        }

        /// <summary>
        /// Clear a slot.
        /// </summary>
        public static void ClearSlot(int slot)
        {
            SetSlot(slot, null);
        }

        /// <summary>
        /// Get all ability slots for a player.
        /// </summary>
        public static Dictionary<int, string> GetAllSlots(Player player)
        {
            var result = new Dictionary<int, string>();
            if (player == null) return result;

            var data = VikingDataStore.Get(player);
            if (data == null) return result;

            return new Dictionary<int, string>(data.AbilitySlots);
        }

        /// <summary>
        /// Use an ability slot (cast the ability).
        /// </summary>
        public static void UseSlot(Player player, int slot)
        {
            if (player == null || slot < 0 || slot >= SlotCount) return;

            string abilityId = GetSlot(player, slot);
            if (string.IsNullOrEmpty(abilityId))
            {
                Plugin.Log.LogDebug($"Slot {slot} is empty");
                OnAbilityUsed?.Invoke(slot, null, false);
                return;
            }

            // Use Prime to cast the ability
            bool success = false;
            if (Plugin.HasPrime)
            {
                success = TryCastAbilityWithPrime(player, abilityId);
            }
            else
            {
                Plugin.Log.LogWarning($"Prime not available, cannot cast ability: {abilityId}");
            }

            // Raise event for UI feedback
            OnAbilityUsed?.Invoke(slot, abilityId, success);

            if (success)
            {
                Plugin.Log.LogDebug($"Cast ability: {abilityId}");
            }
        }

        /// <summary>
        /// Try to cast an ability using Prime.
        /// Separated to handle soft dependency.
        /// </summary>
        private static bool TryCastAbilityWithPrime(Player player, string abilityId)
        {
            try
            {
                // Check if player has the ability granted
                if (!Prime.PrimeAPI.HasAbility(player, abilityId))
                {
                    Plugin.Log.LogDebug($"Player doesn't have ability: {abilityId}");

                    // Show notification if Veneer is available
                    if (Plugin.HasVeneer)
                    {
                        ShowAbilityNotGrantedNotification(abilityId);
                    }
                    return false;
                }

                // Try to use the ability
                bool result = Prime.PrimeAPI.UseAbility(player, abilityId);

                if (!result && Plugin.HasVeneer)
                {
                    // Check why it failed
                    var abilities = Prime.PrimeAPI.GetGrantedAbilities(player);
                    foreach (var ability in abilities)
                    {
                        if (ability.Definition.Id == abilityId)
                        {
                            if (ability.State == Prime.Abilities.AbilityState.OnCooldown)
                            {
                                ShowAbilityOnCooldownNotification(abilityId, ability.GetRemainingCooldown());
                            }
                            else if (!ability.HasResources())
                            {
                                ShowNotEnoughResourcesNotification(abilityId);
                            }
                            break;
                        }
                    }
                }

                return result;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Failed to cast ability {abilityId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the cooldown progress for an ability in a slot (0-1, 1 = ready).
        /// </summary>
        public static float GetCooldownProgress(Player player, int slot)
        {
            if (!Plugin.HasPrime) return 1f;
            if (player == null || slot < 0 || slot >= SlotCount) return 1f;

            string abilityId = GetSlot(player, slot);
            if (string.IsNullOrEmpty(abilityId)) return 1f;

            try
            {
                var abilities = Prime.PrimeAPI.GetGrantedAbilities(player);
                foreach (var ability in abilities)
                {
                    if (ability.Definition.Id == abilityId)
                    {
                        return ability.GetCooldownProgress();
                    }
                }
            }
            catch { }

            return 1f;
        }

        /// <summary>
        /// Get remaining cooldown time for an ability in a slot.
        /// </summary>
        public static float GetRemainingCooldown(Player player, int slot)
        {
            if (!Plugin.HasPrime) return 0f;
            if (player == null || slot < 0 || slot >= SlotCount) return 0f;

            string abilityId = GetSlot(player, slot);
            if (string.IsNullOrEmpty(abilityId)) return 0f;

            try
            {
                var abilities = Prime.PrimeAPI.GetGrantedAbilities(player);
                foreach (var ability in abilities)
                {
                    if (ability.Definition.Id == abilityId)
                    {
                        return ability.GetRemainingCooldown();
                    }
                }
            }
            catch { }

            return 0f;
        }

        /// <summary>
        /// Check if an ability in a slot can be cast.
        /// </summary>
        public static bool CanCast(Player player, int slot)
        {
            if (!Plugin.HasPrime) return false;
            if (player == null || slot < 0 || slot >= SlotCount) return false;

            string abilityId = GetSlot(player, slot);
            if (string.IsNullOrEmpty(abilityId)) return false;

            try
            {
                var abilities = Prime.PrimeAPI.GetGrantedAbilities(player);
                foreach (var ability in abilities)
                {
                    if (ability.Definition.Id == abilityId)
                    {
                        return ability.CanCast();
                    }
                }
            }
            catch { }

            return false;
        }

        #region Notifications

        private static void ShowAbilityNotGrantedNotification(string abilityId)
        {
            ShowCenterMessage($"Ability not learned: {abilityId}");
        }

        private static void ShowAbilityOnCooldownNotification(string abilityId, float remaining)
        {
            ShowCenterMessage($"{abilityId} on cooldown ({remaining:F1}s)");
        }

        private static void ShowNotEnoughResourcesNotification(string abilityId)
        {
            ShowCenterMessage($"Not enough resources for {abilityId}");
        }

        private static void ShowCenterMessage(string message)
        {
            // Use Valheim's built-in MessageHud
            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, message);
            }
        }

        #endregion

        /// <summary>
        /// Check keybind input and use ability slots.
        /// Call this from Update.
        /// </summary>
        public static void CheckInput()
        {
            if (Player.m_localPlayer == null) return;
            if (Chat.instance != null && Chat.instance.HasFocus()) return;
            if (Console.IsVisible()) return;
            if (TextInput.IsVisible()) return;
            if (Menu.IsVisible()) return;
            if (InventoryGui.IsVisible()) return;

            for (int i = 0; i < SlotCount && i < DefaultKeybinds.Length; i++)
            {
                if (Input.GetKeyDown(DefaultKeybinds[i]))
                {
                    UseSlot(Player.m_localPlayer, i);
                }
            }
        }
    }
}
