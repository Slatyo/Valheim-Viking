using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Veneer.Components.Primitives;
using Veneer.Theme;
using Viking.Talents;

namespace Viking.UI
{
    /// <summary>
    /// UI element for a single talent node.
    /// Nodes are circular with distinct borders for each type.
    /// </summary>
    public class TalentNodeUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private TalentNode _node;
        private Image _background;
        private Image _border;
        private Image _innerCircle;
        private Image _highlight;
        private Image _glow;
        private VeneerText _rankText;

        private bool _isAllocated;
        private bool _canAllocate;
        private int _currentRanks;
        private int _maxRanks;

        private Color _baseBorderColor;
        private Color _baseGlowColor;

        // Node sizes - larger for better visibility
        private const float SIZE_MINOR = 44f;
        private const float SIZE_NOTABLE = 56f;
        private const float SIZE_KEYSTONE = 72f;
        private const float SIZE_START = 60f;

        // Border widths - thicker for visibility
        private const int BORDER_MINOR = 3;
        private const int BORDER_NOTABLE = 4;
        private const int BORDER_KEYSTONE = 5;
        private const int BORDER_START = 4;

        // Texture resolution
        private const int TEXTURE_SIZE = 64;

        /// <summary>Event fired when node is clicked.</summary>
        public event Action<TalentNode> OnClicked;

        /// <summary>Event fired when node is hovered.</summary>
        public event Action<TalentNode> OnHovered;

        /// <summary>Event fired when node is unhovered.</summary>
        public event Action<TalentNode> OnUnhovered;

        /// <summary>
        /// Create a talent node UI.
        /// </summary>
        public static TalentNodeUI Create(Transform parent, TalentNode node)
        {
            float size = node.Type switch
            {
                TalentNodeType.Minor => SIZE_MINOR,
                TalentNodeType.Notable => SIZE_NOTABLE,
                TalentNodeType.Keystone => SIZE_KEYSTONE,
                TalentNodeType.Start => SIZE_START,
                _ => SIZE_MINOR
            };

            var go = new GameObject($"Node_{node.Id}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var nodeUI = go.AddComponent<TalentNodeUI>();
            nodeUI._node = node;
            nodeUI.Initialize(size);

            return nodeUI;
        }

        private void Initialize(float size)
        {
            int borderWidth = _node.Type switch
            {
                TalentNodeType.Keystone => BORDER_KEYSTONE,
                TalentNodeType.Notable => BORDER_NOTABLE,
                TalentNodeType.Start => BORDER_START,
                _ => BORDER_MINOR
            };

            // Determine colors based on node type
            _baseBorderColor = _node.Type switch
            {
                TalentNodeType.Keystone => new Color(1f, 0.6f, 0.1f, 1f),    // Orange-gold
                TalentNodeType.Notable => new Color(0.4f, 0.6f, 1f, 1f),     // Blue
                TalentNodeType.Start => new Color(0.3f, 1f, 0.5f, 1f),       // Green
                _ => new Color(0.6f, 0.6f, 0.6f, 1f)                          // Gray
            };

            _baseGlowColor = _node.Type switch
            {
                TalentNodeType.Keystone => new Color(1f, 0.5f, 0f, 0.5f),
                TalentNodeType.Notable => new Color(0.3f, 0.5f, 1f, 0.4f),
                TalentNodeType.Start => new Color(0.2f, 0.8f, 0.4f, 0.4f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.3f)
            };

            // Outer glow (for allocated/special nodes)
            var glowGo = new GameObject("Glow", typeof(RectTransform));
            glowGo.transform.SetParent(transform, false);
            var glowRect = glowGo.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-8, -8);
            glowRect.offsetMax = new Vector2(8, 8);

            _glow = glowGo.AddComponent<Image>();
            _glow.sprite = VeneerTextures.CreateCircleSprite(TEXTURE_SIZE, Color.clear, _baseGlowColor, 12);
            _glow.color = Color.clear; // Initially hidden
            _glow.raycastTarget = false;

            // Border ring (outer colored ring)
            var borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(transform, false);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            _border = borderGo.AddComponent<Image>();
            _border.sprite = VeneerTextures.CreateCircleRingSprite(TEXTURE_SIZE, _baseBorderColor, borderWidth + 2);
            _border.raycastTarget = false;

            // Background circle (main fill)
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(borderWidth, borderWidth);
            bgRect.offsetMax = new Vector2(-borderWidth, -borderWidth);

            _background = bgGo.AddComponent<Image>();
            _background.sprite = VeneerTextures.CreateCircleSprite(TEXTURE_SIZE, new Color(0.12f, 0.12f, 0.14f, 1f), Color.clear, 0);
            _background.raycastTarget = false; // Visual only - we use a separate raycast target

            // Add invisible raycast target that covers the whole node area
            // This ensures clicks register regardless of circle sprite transparency
            var raycastGo = new GameObject("RaycastTarget", typeof(RectTransform));
            raycastGo.transform.SetParent(transform, false);
            var raycastRect = raycastGo.GetComponent<RectTransform>();
            raycastRect.anchorMin = Vector2.zero;
            raycastRect.anchorMax = Vector2.one;
            raycastRect.offsetMin = Vector2.zero;
            raycastRect.offsetMax = Vector2.zero;

            var raycastImage = raycastGo.AddComponent<Image>();
            raycastImage.color = Color.clear; // Invisible
            raycastImage.raycastTarget = true; // Catches all clicks in the node's bounds

            // Inner circle (icon/content area)
            var innerGo = new GameObject("Inner", typeof(RectTransform));
            innerGo.transform.SetParent(transform, false);
            var innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.2f, 0.2f);
            innerRect.anchorMax = new Vector2(0.8f, 0.8f);
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;

            _innerCircle = innerGo.AddComponent<Image>();
            _innerCircle.sprite = VeneerTextures.CreateCircleSprite(TEXTURE_SIZE, new Color(0.18f, 0.18f, 0.2f, 1f), Color.clear, 0);
            _innerCircle.raycastTarget = false;

            // Rank text (for multi-rank nodes)
            if (_node.MaxRanks > 1)
            {
                _rankText = VeneerText.Create(transform, "0/0");
                _rankText.ApplyStyle(TextStyle.Caption);
                _rankText.Alignment = TextAnchor.MiddleCenter;
                _rankText.FontSize = 11;
                _rankText.TextColor = Color.white;

                var rankRect = _rankText.GetComponent<RectTransform>();
                rankRect.anchorMin = new Vector2(0.15f, 0.15f);
                rankRect.anchorMax = new Vector2(0.85f, 0.85f);
                rankRect.offsetMin = Vector2.zero;
                rankRect.offsetMax = Vector2.zero;
            }

            // Highlight ring (shows on hover)
            var highlightGo = new GameObject("Highlight", typeof(RectTransform));
            highlightGo.transform.SetParent(transform, false);
            var highlightRect = highlightGo.GetComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = new Vector2(-3, -3);
            highlightRect.offsetMax = new Vector2(3, 3);

            _highlight = highlightGo.AddComponent<Image>();
            _highlight.sprite = VeneerTextures.CreateCircleRingSprite(TEXTURE_SIZE, Color.white, 2);
            _highlight.color = Color.clear; // Initially invisible
            _highlight.raycastTarget = false;

            // Initial state
            UpdateState(0, _node.MaxRanks, false, false);
        }

        /// <summary>
        /// Update the node's visual state.
        /// </summary>
        public void UpdateState(int currentRanks, int maxRanks, bool isAllocated, bool canAllocate)
        {
            _currentRanks = currentRanks;
            _maxRanks = maxRanks;
            _isAllocated = isAllocated;
            _canAllocate = canAllocate;

            // Update rank text
            if (_rankText != null)
            {
                _rankText.Content = $"{currentRanks}/{maxRanks}";
            }

            // Update colors based on state
            if (_isAllocated)
            {
                // Allocated - green tint with glow
                _background.color = new Color(0.15f, 0.28f, 0.15f, 1f);
                _innerCircle.color = new Color(0.2f, 0.4f, 0.2f, 1f);

                // Green border
                _border.sprite = VeneerTextures.CreateCircleRingSprite(TEXTURE_SIZE, new Color(0.4f, 1f, 0.5f, 1f), GetBorderWidth() + 2);

                // Show glow
                _glow.color = new Color(0.3f, 1f, 0.4f, 0.6f);
            }
            else if (_canAllocate)
            {
                // Can allocate - golden highlight
                _background.color = new Color(0.2f, 0.18f, 0.1f, 1f);
                _innerCircle.color = new Color(0.28f, 0.25f, 0.15f, 1f);

                // Golden border
                _border.sprite = VeneerTextures.CreateCircleRingSprite(TEXTURE_SIZE, new Color(1f, 0.85f, 0.3f, 1f), GetBorderWidth() + 2);

                // Subtle golden glow
                _glow.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            }
            else
            {
                // Locked/unavailable - dimmed
                _background.color = new Color(0.1f, 0.1f, 0.11f, 1f);
                _innerCircle.color = new Color(0.14f, 0.14f, 0.15f, 1f);

                // Dimmed border color
                var dimBorder = new Color(_baseBorderColor.r * 0.4f, _baseBorderColor.g * 0.4f, _baseBorderColor.b * 0.4f, 0.6f);
                _border.sprite = VeneerTextures.CreateCircleRingSprite(TEXTURE_SIZE, dimBorder, GetBorderWidth() + 2);

                // No glow
                _glow.color = Color.clear;
            }
        }

        private int GetBorderWidth()
        {
            return _node.Type switch
            {
                TalentNodeType.Keystone => BORDER_KEYSTONE,
                TalentNodeType.Notable => BORDER_NOTABLE,
                TalentNodeType.Start => BORDER_START,
                _ => BORDER_MINOR
            };
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnClicked?.Invoke(_node);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Show white highlight ring
            _highlight.color = new Color(1, 1, 1, 0.8f);

            // Brighten existing glow
            if (_isAllocated)
            {
                _glow.color = new Color(0.4f, 1f, 0.5f, 0.8f);
            }
            else if (_canAllocate)
            {
                _glow.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            }
            else
            {
                // Show subtle glow even on locked nodes when hovered
                _glow.color = new Color(_baseBorderColor.r, _baseBorderColor.g, _baseBorderColor.b, 0.3f);
            }

            OnHovered?.Invoke(_node);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Hide highlight
            _highlight.color = Color.clear;

            // Reset glow to normal state
            if (_isAllocated)
            {
                _glow.color = new Color(0.3f, 1f, 0.4f, 0.6f);
            }
            else if (_canAllocate)
            {
                _glow.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            }
            else
            {
                _glow.color = Color.clear;
            }

            OnUnhovered?.Invoke(_node);
        }
    }
}
