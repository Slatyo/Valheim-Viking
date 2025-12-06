using UnityEngine;
using Veneer.Core;
using Viking.Core;
using Viking.UI;

namespace Viking.Integration
{
    /// <summary>
    /// Integration with Veneer for UI components.
    /// </summary>
    public static class VeneerIntegration
    {
        private static TalentTreeWindow _talentTreeWindow;
        private static AbilityBarUI _abilityBarUI;
        private static CharacterWindow _characterWindow;
        private static VikingQuickBarExtension _quickBarExtension;

        /// <summary>Key to toggle talent tree UI.</summary>
        public static KeyCode TalentTreeKey = KeyCode.K;

        /// <summary>
        /// Initialize Veneer integration.
        /// </summary>
        public static void Initialize()
        {
            // IMPORTANT: Disable VeneerHotbar IMMEDIATELY before Veneer creates HUD
            // Viking provides its own ability bar replacement
            VeneerAPI.DisableHotbar();
            _hotbarHidden = true;
            Plugin.Log.LogInfo("Disabled VeneerHotbar - Viking will provide action bar");

            // Wait for Veneer to be ready to create our UI
            if (VeneerAPI.IsReady)
            {
                OnVeneerReady();
            }
            else
            {
                VeneerAPI.OnReady += OnVeneerReady;
            }

            Plugin.Log.LogInfo("Veneer integration initialized");
        }

        /// <summary>
        /// Called when Veneer is ready.
        /// </summary>
        private static void OnVeneerReady()
        {
            VeneerAPI.OnReady -= OnVeneerReady;

            Plugin.Log.LogInfo($"OnVeneerReady called. HudRoot={VeneerAPI.HudRoot?.name ?? "null"}, UIRoot={VeneerAPI.UIRoot?.name ?? "null"}");

            // Register quickbar extension for Character button (this can happen immediately)
            _quickBarExtension = new VikingQuickBarExtension();
            VeneerAPI.RegisterQuickBarExtension(_quickBarExtension);

            // AbilityBarUI needs VeneerHud which isn't created yet
            // It will be created when the player spawns (in CheckInput or via coroutine)
            _abilityBarPending = true;

            Plugin.Log.LogInfo("Veneer integration ready - AbilityBar will be created when HUD is available");
        }

        private static bool _abilityBarPending = false;

        /// <summary>
        /// Try to create the ability bar if it's pending and HudRoot is available.
        /// Called from CheckInput.
        /// </summary>
        private static void TryCreateAbilityBar()
        {
            if (!_abilityBarPending) return;
            if (_abilityBarUI != null) return;

            // Check if HudRoot is available now
            var hudRoot = VeneerAPI.HudRoot;
            if (hudRoot == null) return;

            Plugin.Log.LogInfo($"HudRoot now available: {hudRoot.name}");

            CreateAbilityBarUI();

            if (_abilityBarUI != null)
            {
                Plugin.Log.LogInfo("Viking AbilityBarUI created successfully (delayed)");
                _abilityBarPending = false;
            }
        }

        /// <summary>
        /// Tracks whether the hotbar has been disabled.
        /// </summary>
        private static bool _hotbarHidden = false;

        /// <summary>
        /// Cleanup Veneer integration.
        /// </summary>
        public static void Cleanup()
        {
            VeneerAPI.OnReady -= OnVeneerReady;

            // Re-enable VeneerHotbar if we disabled it
            if (_hotbarHidden)
            {
                VeneerAPI.EnableHotbar();
                _hotbarHidden = false;
            }

            if (_quickBarExtension != null)
            {
                VeneerAPI.UnregisterQuickBarExtension(_quickBarExtension);
                _quickBarExtension = null;
            }

            if (_talentTreeWindow != null)
            {
                Object.Destroy(_talentTreeWindow.gameObject);
                _talentTreeWindow = null;
            }

            if (_abilityBarUI != null)
            {
                Object.Destroy(_abilityBarUI.gameObject);
                _abilityBarUI = null;
            }

            if (_characterWindow != null)
            {
                Object.Destroy(_characterWindow.gameObject);
                _characterWindow = null;
            }
        }

        /// <summary>
        /// Check for UI input.
        /// </summary>
        public static void CheckInput()
        {
            // Try to create ability bar if pending (needs HudRoot which may not exist at startup)
            TryCreateAbilityBar();

            if (Player.m_localPlayer == null) return;
            if (Chat.instance != null && Chat.instance.HasFocus()) return;
            if (Console.IsVisible()) return;
            if (TextInput.IsVisible()) return;
            if (Menu.IsVisible()) return;

            // Toggle talent tree with K key
            if (Input.GetKeyDown(TalentTreeKey))
            {
                ToggleTalentTree();
            }
        }

        /// <summary>
        /// Toggle the talent tree window.
        /// </summary>
        public static void ToggleTalentTree()
        {
            if (_talentTreeWindow == null)
            {
                CreateTalentTreeWindow();
            }

            if (_talentTreeWindow.IsVisible)
            {
                _talentTreeWindow.Hide();
            }
            else
            {
                _talentTreeWindow.Show();
                _talentTreeWindow.Refresh();
            }
        }

        /// <summary>
        /// Open the talent tree window.
        /// </summary>
        public static void OpenTalentTree()
        {
            if (_talentTreeWindow == null)
            {
                CreateTalentTreeWindow();
            }

            _talentTreeWindow.Show();
            _talentTreeWindow.Refresh();
        }

        /// <summary>
        /// Close the talent tree window.
        /// </summary>
        public static void CloseTalentTree()
        {
            _talentTreeWindow?.Hide();
        }

        /// <summary>
        /// Refresh the talent tree UI.
        /// </summary>
        public static void RefreshTalentTree()
        {
            _talentTreeWindow?.Refresh();
        }

        /// <summary>
        /// Refresh the ability bar UI.
        /// </summary>
        public static void RefreshAbilityBar()
        {
            _abilityBarUI?.Refresh();
        }

        /// <summary>
        /// Toggle the character window.
        /// </summary>
        public static void ToggleCharacterWindow()
        {
            if (_characterWindow == null)
            {
                CreateCharacterWindow();
            }

            _characterWindow.Toggle();
        }

        /// <summary>
        /// Open the character window.
        /// </summary>
        public static void OpenCharacterWindow()
        {
            if (_characterWindow == null)
            {
                CreateCharacterWindow();
            }

            _characterWindow.Show();
        }

        /// <summary>
        /// Close the character window.
        /// </summary>
        public static void CloseCharacterWindow()
        {
            _characterWindow?.Hide();
        }

        /// <summary>
        /// Refresh the character window.
        /// </summary>
        public static void RefreshCharacterWindow()
        {
            _characterWindow?.Refresh();
        }

        /// <summary>
        /// Create the talent tree window.
        /// </summary>
        private static void CreateTalentTreeWindow()
        {
            _talentTreeWindow = TalentTreeWindow.Create();
        }

        /// <summary>
        /// Create the ability bar UI.
        /// </summary>
        private static void CreateAbilityBarUI()
        {
            _abilityBarUI = AbilityBarUI.Create();
        }

        /// <summary>
        /// Create the character window.
        /// </summary>
        private static void CreateCharacterWindow()
        {
            _characterWindow = CharacterWindow.Create();
        }

        /// <summary>
        /// Show a notification using Veneer.
        /// </summary>
        public static void ShowNotification(string message, NotificationType type = NotificationType.Info)
        {
            // TODO: Implement when Veneer has notification system
            Plugin.Log.LogInfo($"[Notification] {type}: {message}");
        }
    }

    /// <summary>
    /// Notification type.
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
