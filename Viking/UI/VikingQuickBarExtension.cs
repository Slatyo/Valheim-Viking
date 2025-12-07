using Veneer.Components.Base;
using Veneer.Components.Primitives;
using Veneer.Core;
using Veneer.Extensions;
using Veneer.Vanilla.Replacements;
using Viking.Integration;

namespace Viking.UI
{
    /// <summary>
    /// Quickbar extension that adds a Character button.
    /// </summary>
    public class VikingQuickBarExtension : IQuickBarExtension
    {
        public int Priority => 100;
        public string ExtensionId => "viking.character_button";

        private VeneerButton _button;

        public void OnQuickBarCreated(QuickBarContext context)
        {
            if (context?.ButtonContainer == null) return;

            _button = VeneerQuickBar.CreateQuickBarButton(
                context.ButtonContainer,
                "Character",
                () => VeneerIntegration.ToggleCharacterWindow()
            );

            // Subscribe to window manager events to update button style
            VeneerWindowManager.OnWindowOpened += OnWindowStateChanged;
            VeneerWindowManager.OnWindowClosed += OnWindowStateChanged;

            UpdateButtonStyle();
            Plugin.Log.LogInfo("Viking quickbar button added");
        }

        public void OnQuickBarDestroyed()
        {
            VeneerWindowManager.OnWindowOpened -= OnWindowStateChanged;
            VeneerWindowManager.OnWindowClosed -= OnWindowStateChanged;
            _button = null;
        }

        private void OnWindowStateChanged(VeneerElement window)
        {
            UpdateButtonStyle();
        }

        private void UpdateButtonStyle()
        {
            if (_button == null) return;

            bool isVisible = VeneerIntegration.IsCharacterWindowVisible();
            _button.SetStyle(isVisible ? ButtonStyle.TabActive : ButtonStyle.Tab);
        }
    }
}
