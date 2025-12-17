using UnityEngine;
using UnityEngine.UI;
using Veneer.Components.Base;
using Veneer.Core;
using Veneer.Grid;
using Veneer.Theme;

namespace Viking.UI
{
    /// <summary>
    /// Prime-integrated player frame with clean resource bars.
    /// Replaces VeneerUnitFrame when Viking is loaded.
    /// </summary>
    public class VikingPlayerFrame : VeneerElement
    {
        private const string ElementId_PlayerFrame = "Viking_PlayerFrame";

        // Resource bars
        private ResourceBar _healthBar;
        private ResourceBar _staminaBar;
        private ResourceBar _eitrBar;
        private Text _nameText;

        private RectTransform _contentRect;
        private Player _trackedPlayer;
        private bool _showEitr;

        /// <summary>
        /// The player being tracked.
        /// </summary>
        public Player TrackedPlayer => _trackedPlayer;

        /// <summary>
        /// Creates the Viking player frame.
        /// </summary>
        public static VikingPlayerFrame Create(Transform parent)
        {
            var go = CreateUIObject("VikingPlayerFrame", parent);
            var frame = go.AddComponent<VikingPlayerFrame>();
            frame.Initialize();
            return frame;
        }

        private void Initialize()
        {
            ElementId = ElementId_PlayerFrame;
            IsMoveable = true;
            SavePosition = true;
            LayerType = VeneerLayerType.HUD;

            // Register with anchor system
            VeneerAnchor.Register(ElementId, ScreenAnchor.BottomLeft, new Vector2(20, 100));

            // Frame dimensions - compact bar layout
            float frameWidth = 200f;
            float frameHeight = 80f;
            float padding = 6f;

            SetSize(frameWidth, frameHeight);
            AnchorTo(AnchorPreset.BottomLeft, new Vector2(20, 100));

            // Main background
            var bgImage = gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            bgImage.sprite = VeneerTextures.CreatePanelSprite();
            bgImage.type = Image.Type.Sliced;

            // Accent border
            var borderGo = CreateUIObject("Border", transform);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImage = borderGo.AddComponent<Image>();
            var borderTex = VeneerTextures.CreateSlicedBorderTexture(16, new Color(0.5f, 0.45f, 0.3f, 0.5f), Color.clear, 1);
            borderImage.sprite = VeneerTextures.CreateSlicedSprite(borderTex, 1);
            borderImage.type = Image.Type.Sliced;
            borderImage.raycastTarget = false;

            // Content container
            var contentGo = CreateUIObject("Content", transform);
            _contentRect = contentGo.GetComponent<RectTransform>();
            _contentRect.anchorMin = Vector2.zero;
            _contentRect.anchorMax = Vector2.one;
            _contentRect.offsetMin = new Vector2(padding, padding);
            _contentRect.offsetMax = new Vector2(-padding, -padding);

            // Player name at top
            CreateNameDisplay();

            // Resource bars
            CreateResourceBars();

            // Add mover for edit mode
            var mover = gameObject.AddComponent<VeneerMover>();
            mover.ElementId = ElementId;

            // Add resizer
            var resizer = gameObject.AddComponent<VeneerResizer>();
            resizer.MinSize = new Vector2(150, 60);
            resizer.MaxSize = new Vector2(350, 120);

            // Apply saved position
            var savedData = VeneerAnchor.GetAnchorData(ElementId);
            if (savedData != null)
            {
                VeneerAnchor.ApplyAnchor(RectTransform, savedData.Anchor, savedData.Offset);
            }
        }

        private void CreateNameDisplay()
        {
            var nameGo = CreateUIObject("PlayerName", _contentRect);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0.5f, 1);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0, 16);

            _nameText = nameGo.AddComponent<Text>();
            _nameText.text = "Player";
            _nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _nameText.fontSize = 12;
            _nameText.fontStyle = FontStyle.Bold;
            _nameText.color = VeneerColors.TextGold;
            _nameText.alignment = TextAnchor.MiddleCenter;
            _nameText.raycastTarget = false;
        }

        private void CreateResourceBars()
        {
            // Container for bars
            var barsGo = CreateUIObject("ResourceBars", _contentRect);
            var barsRect = barsGo.GetComponent<RectTransform>();
            barsRect.anchorMin = new Vector2(0, 0);
            barsRect.anchorMax = new Vector2(1, 1);
            barsRect.offsetMin = new Vector2(0, 0);
            barsRect.offsetMax = new Vector2(0, -18); // Leave room for name

            float barHeight = 16f;
            float barSpacing = 2f;

            // Health bar - top, red
            _healthBar = ResourceBar.Create(barsRect, "Health",
                new Color(0.75f, 0.2f, 0.2f, 1f),
                new Color(0.3f, 0.08f, 0.08f, 0.9f));
            var healthRect = _healthBar.GetComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0, 1);
            healthRect.anchorMax = new Vector2(1, 1);
            healthRect.pivot = new Vector2(0.5f, 1);
            healthRect.anchoredPosition = new Vector2(0, 0);
            healthRect.sizeDelta = new Vector2(0, barHeight);

            // Stamina bar - middle, yellow
            _staminaBar = ResourceBar.Create(barsRect, "Stamina",
                new Color(0.85f, 0.7f, 0.2f, 1f),
                new Color(0.35f, 0.3f, 0.1f, 0.9f));
            var staminaRect = _staminaBar.GetComponent<RectTransform>();
            staminaRect.anchorMin = new Vector2(0, 1);
            staminaRect.anchorMax = new Vector2(1, 1);
            staminaRect.pivot = new Vector2(0.5f, 1);
            staminaRect.anchoredPosition = new Vector2(0, -(barHeight + barSpacing));
            staminaRect.sizeDelta = new Vector2(0, barHeight);

            // Eitr bar - bottom, blue (hidden by default)
            _eitrBar = ResourceBar.Create(barsRect, "Eitr",
                new Color(0.3f, 0.45f, 0.85f, 1f),
                new Color(0.12f, 0.18f, 0.35f, 0.9f));
            var eitrRect = _eitrBar.GetComponent<RectTransform>();
            eitrRect.anchorMin = new Vector2(0, 1);
            eitrRect.anchorMax = new Vector2(1, 1);
            eitrRect.pivot = new Vector2(0.5f, 1);
            eitrRect.anchoredPosition = new Vector2(0, -2 * (barHeight + barSpacing));
            eitrRect.sizeDelta = new Vector2(0, barHeight);
            _eitrBar.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_trackedPlayer == null)
            {
                _trackedPlayer = Player.m_localPlayer;
                if (_trackedPlayer != null && _nameText != null)
                {
                    _nameText.text = _trackedPlayer.GetPlayerName();
                }
            }

            if (_trackedPlayer == null) return;

            UpdateResources();
        }

        private void UpdateResources()
        {
            // Use vanilla getters which are patched by Prime
            float health = _trackedPlayer.GetHealth();
            float maxHealth = _trackedPlayer.GetMaxHealth();
            float stamina = _trackedPlayer.GetStamina();
            float maxStamina = _trackedPlayer.GetMaxStamina();
            float eitr = _trackedPlayer.GetEitr();
            float maxEitr = _trackedPlayer.GetMaxEitr();

            // Update bars
            _healthBar?.SetValues(health, maxHealth);
            _staminaBar?.SetValues(stamina, maxStamina);

            // Handle eitr visibility
            bool hasEitr = maxEitr > 0;
            if (hasEitr != _showEitr)
            {
                _showEitr = hasEitr;
                _eitrBar?.gameObject.SetActive(hasEitr);
            }

            if (hasEitr)
            {
                _eitrBar?.SetValues(eitr, maxEitr);
            }
        }

        /// <summary>
        /// Sets the player to track.
        /// </summary>
        public void SetPlayer(Player player)
        {
            _trackedPlayer = player;
            if (_nameText != null && player != null)
            {
                _nameText.text = player.GetPlayerName();
            }
        }
    }

    /// <summary>
    /// Helper class for creating UI objects.
    /// </summary>
    internal static class UIHelper
    {
        public static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }
    }

    /// <summary>
    /// Clean horizontal resource bar with smooth fill animation.
    /// </summary>
    public class ResourceBar : MonoBehaviour
    {
        private Image _fillImage;
        private Text _valueText;
        private float _currentFill;
        private float _targetFill;

        public static ResourceBar Create(Transform parent, string name, Color fillColor, Color bgColor)
        {
            var go = UIHelper.CreateUIObject($"{name}Bar", parent);
            var bar = go.AddComponent<ResourceBar>();
            bar.Initialize(fillColor, bgColor);
            return bar;
        }

        private void Initialize(Color fillColor, Color bgColor)
        {
            // Background
            var bgImage = gameObject.AddComponent<Image>();
            bgImage.color = bgColor;
            bgImage.raycastTarget = false;

            // Border
            var borderGo = UIHelper.CreateUIObject("Border", transform);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImage = borderGo.AddComponent<Image>();
            var borderTex = VeneerTextures.CreateSlicedBorderTexture(8, new Color(0.3f, 0.3f, 0.3f, 0.6f), Color.clear, 1);
            borderImage.sprite = VeneerTextures.CreateSlicedSprite(borderTex, 1);
            borderImage.type = Image.Type.Sliced;
            borderImage.raycastTarget = false;

            // Fill bar
            var fillGo = UIHelper.CreateUIObject("Fill", transform);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);

            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = fillColor;
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = 0;
            _fillImage.fillAmount = 1f;
            _fillImage.raycastTarget = false;

            // Value text
            var textGo = UIHelper.CreateUIObject("Value", transform);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-4, 0);

            _valueText = textGo.AddComponent<Text>();
            _valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _valueText.fontSize = 10;
            _valueText.fontStyle = FontStyle.Bold;
            _valueText.color = Color.white;
            _valueText.alignment = TextAnchor.MiddleCenter;
            _valueText.raycastTarget = false;

            // Outline for readability
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1, -1);
        }

        public void SetValues(float current, float max)
        {
            _targetFill = max > 0 ? Mathf.Clamp01(current / max) : 0f;

            // Smooth fill animation
            if (Mathf.Abs(_currentFill - _targetFill) > 0.001f)
            {
                _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * 10f);
            }
            else
            {
                _currentFill = _targetFill;
            }

            if (_fillImage != null)
            {
                _fillImage.fillAmount = _currentFill;
            }

            // Show current / max
            _valueText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
        }
    }
}
