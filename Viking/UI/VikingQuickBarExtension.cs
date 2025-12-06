using UnityEngine;
using UnityEngine.UI;
using Veneer.Components.Primitives;
using Veneer.Extensions;
using Veneer.Theme;
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

        private GameObject _characterButton;

        public void OnQuickBarCreated(QuickBarContext context)
        {
            if (context?.ButtonContainer == null) return;

            // Create Character button
            _characterButton = new GameObject("VikingCharacterBtn", typeof(RectTransform));
            _characterButton.transform.SetParent(context.ButtonContainer, false);

            // Add layout element
            var layout = _characterButton.AddComponent<LayoutElement>();
            layout.preferredWidth = 32;
            layout.preferredHeight = 32;

            // Add background
            var bg = _characterButton.AddComponent<Image>();
            bg.color = VeneerColors.BackgroundLight;

            // Add button
            var button = _characterButton.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(OnCharacterButtonClicked);

            // Add hover color change
            var colors = button.colors;
            colors.normalColor = VeneerColors.BackgroundLight;
            colors.highlightedColor = VeneerColors.BackgroundSolid;
            colors.pressedColor = VeneerColors.Accent;
            button.colors = colors;

            // Add text label
            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(_characterButton.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<Text>();
            text.text = "Character";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 11;
            text.fontStyle = FontStyle.Bold;
            text.color = VeneerColors.TextGold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            Plugin.Log.LogInfo("Viking quickbar button added");
        }

        public void OnQuickBarDestroyed()
        {
            if (_characterButton != null)
            {
                Object.Destroy(_characterButton);
                _characterButton = null;
            }
        }

        private void OnCharacterButtonClicked()
        {
            VeneerIntegration.ToggleCharacterWindow();
        }
    }
}
