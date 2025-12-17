using UnityEngine;
using Veneer.Extensions;
using Veneer.Vanilla.Replacements;

namespace Viking.UI
{
    /// <summary>
    /// HUD extension that replaces Veneer's default PlayerFrame with a Prime-integrated version.
    /// Also creates the XP bar as a separate positionable element.
    /// </summary>
    public class VikingHudExtension : IHudExtension
    {
        public string ExtensionId => "viking.hud";
        public int Priority => -100;  // Run before other extensions

        private VikingPlayerFrame _playerFrame;
        private VikingXPBar _xpBar;

        /// <summary>
        /// Called when the HUD is created. Replaces Veneer's PlayerFrame with our own.
        /// </summary>
        public void OnHudCreated(HudContext context)
        {
            // Completely replace Veneer's default PlayerFrame
            var veneerHud = VeneerHud.Instance;
            if (veneerHud?.PlayerFrame != null)
            {
                var playerFrame = veneerHud.PlayerFrame;

                // Remove VeneerMover component so it's excluded from edit mode
                var mover = playerFrame.GetComponent<Veneer.Grid.VeneerMover>();
                if (mover != null)
                {
                    Object.Destroy(mover);
                }

                // Destroy the entire VeneerUnitFrame - Viking replaces it completely
                Object.Destroy(playerFrame.gameObject);
                Plugin.Log.LogInfo("Destroyed VeneerUnitFrame - Viking providing Prime-integrated replacement");
            }

            // Create Viking's Prime-integrated PlayerFrame
            if (context.BottomLeftContainer != null)
            {
                _playerFrame = VikingPlayerFrame.Create(context.BottomLeftContainer);
                Plugin.Log.LogInfo("Created VikingPlayerFrame (Prime-integrated)");
            }
            else
            {
                Plugin.Log.LogWarning("BottomLeftContainer is null, cannot create VikingPlayerFrame");
            }

            // Create XP bar as separate element (uses VeneerAnchor to position at bottom center)
            if (context.HudRoot != null)
            {
                _xpBar = VikingXPBar.Create(context.HudRoot);
                Plugin.Log.LogInfo("Created VikingXPBar");
            }
        }

        /// <summary>
        /// Called when the HUD is destroyed. Cleans up our UI elements.
        /// </summary>
        public void OnHudDestroyed()
        {
            if (_playerFrame != null)
            {
                Object.Destroy(_playerFrame.gameObject);
                _playerFrame = null;
            }

            if (_xpBar != null)
            {
                Object.Destroy(_xpBar.gameObject);
                _xpBar = null;
            }
        }
    }
}
