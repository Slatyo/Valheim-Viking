using UnityEngine;
using UnityEngine.UI;
using Veneer.Components.Base;
using Veneer.Core;
using Veneer.Grid;
using Veneer.Theme;
using Vital.Core;

namespace Viking.UI
{
    /// <summary>
    /// Experience bar showing player level and progress.
    /// Separate VeneerElement for independent positioning/resizing.
    /// </summary>
    public class VikingXPBar : VeneerElement
    {
        private const string ElementId_XPBar = "Viking_XPBar";

        private Image _backgroundImage;
        private Image _fillImage;
        private Text _text;
        private Player _trackedPlayer;

        /// <summary>
        /// Creates the XP bar element.
        /// </summary>
        public static VikingXPBar Create(Transform parent)
        {
            var go = CreateUIObject("VikingXPBar", parent);
            var bar = go.AddComponent<VikingXPBar>();
            bar.Initialize();
            return bar;
        }

        private void Initialize()
        {
            ElementId = ElementId_XPBar;
            IsMoveable = true;
            SavePosition = true;
            LayerType = VeneerLayerType.HUD;

            // Register with anchor system - bottom center by default
            VeneerAnchor.Register(ElementId, ScreenAnchor.BottomCenter, new Vector2(0, 20));

            // Default size
            float barWidth = 300f;
            float barHeight = 18f;

            SetSize(barWidth, barHeight);
            AnchorTo(AnchorPreset.BottomCenter, new Vector2(0, 20));

            // Background
            _backgroundImage = gameObject.AddComponent<Image>();
            _backgroundImage.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            _backgroundImage.raycastTarget = true;

            // Border
            var borderGo = CreateUIObject("Border", transform);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImage = borderGo.AddComponent<Image>();
            var borderTex = VeneerTextures.CreateSlicedBorderTexture(8, new Color(0.4f, 0.35f, 0.25f, 0.7f), Color.clear, 1);
            borderImage.sprite = VeneerTextures.CreateSlicedSprite(borderTex, 1);
            borderImage.type = Image.Type.Sliced;
            borderImage.raycastTarget = false;

            // Fill bar
            var fillGo = CreateUIObject("Fill", transform);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);

            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = new Color(0.25f, 0.45f, 0.75f, 1f);
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = 0;
            _fillImage.fillAmount = 0f;
            _fillImage.raycastTarget = false;

            // Text overlay
            var textGo = CreateUIObject("Text", transform);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            _text = textGo.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.fontSize = 11;
            _text.fontStyle = FontStyle.Bold;
            _text.color = Color.white;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.raycastTarget = false;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1, -1);

            // Add mover for edit mode
            var mover = gameObject.AddComponent<VeneerMover>();
            mover.ElementId = ElementId;

            // Add resizer
            var resizer = gameObject.AddComponent<VeneerResizer>();
            resizer.MinSize = new Vector2(150, 14);
            resizer.MaxSize = new Vector2(600, 30);

            // Apply saved position
            var savedData = VeneerAnchor.GetAnchorData(ElementId);
            if (savedData != null)
            {
                VeneerAnchor.ApplyAnchor(RectTransform, savedData.Anchor, savedData.Offset);
            }
        }

        private void Update()
        {
            if (_trackedPlayer == null)
            {
                _trackedPlayer = Player.m_localPlayer;
            }

            if (_trackedPlayer == null) return;

            UpdateXP();
        }

        private void UpdateXP()
        {
            int level = Leveling.GetLevel(_trackedPlayer);

            // Use the built-in progress calculation which handles edge cases correctly
            float progress = Leveling.GetLevelProgress(_trackedPlayer);

            // Get XP values for display
            long totalXP = Leveling.GetXP(_trackedPlayer);
            long currentLevelXP = Leveling.GetCumulativeXP(level);
            long nextLevelXP = Leveling.GetCumulativeXP(level + 1);
            long xpIntoLevel = totalXP - currentLevelXP;
            long xpNeeded = nextLevelXP - currentLevelXP;

            // Update fill
            _fillImage.fillAmount = Mathf.Clamp01(progress);

            // Update text with level and XP progress
            if (level >= Leveling.MaxLevel)
            {
                _text.text = $"Lv. {level} (Max)";
            }
            else
            {
                _text.text = $"Lv. {level}     {Leveling.FormatXP(xpIntoLevel)} / {Leveling.FormatXP(xpNeeded)}";
            }
        }

        /// <summary>
        /// Set the player to track.
        /// </summary>
        public void SetPlayer(Player player)
        {
            _trackedPlayer = player;
        }
    }
}
