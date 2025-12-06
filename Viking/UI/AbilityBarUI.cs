using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Veneer.Components.Base;
using Veneer.Components.Composite;
using Veneer.Core;
using Veneer.Grid;
using Veneer.Theme;
using Viking.Core;
using Viking.Data;

namespace Viking.UI
{
    /// <summary>
    /// HUD element for the ability bar (8 slots).
    /// Replaces vanilla hotbar with ability slots.
    /// </summary>
    public class AbilityBarUI : VeneerElement
    {
        private const string ElementIdAbilityBar = "Viking_AbilityBar";
        private const int SLOT_COUNT = 8;

        private AbilitySlotUI[] _slots;
        private Image _backgroundImage;
        private Image _borderImage;

        /// <summary>
        /// Create the ability bar UI as a HUD element.
        /// </summary>
        public static AbilityBarUI Create()
        {
            // Parent to Veneer's HUD root (same as VeneerHotbar)
            // Must use HudRoot, not UIRoot - UIRoot is CustomGUIFront which doesn't display properly
            var parent = VeneerAPI.HudRoot;
            if (parent == null)
            {
                Plugin.Log.LogDebug("AbilityBarUI: VeneerAPI.HudRoot is null, cannot create yet");
                return null;
            }

            var go = CreateUIObject("VikingAbilityBar", parent);
            var bar = go.AddComponent<AbilityBarUI>();
            bar.Initialize();

            Plugin.Log.LogInfo($"AbilityBarUI created, parent: {parent.name}");
            return bar;
        }

        private void Initialize()
        {
            // Configure as HUD element (like VeneerHotbar)
            ElementId = ElementIdAbilityBar;
            IsMoveable = true;
            SavePosition = true;
            LayerType = VeneerLayerType.HUD;

            // Register with anchor system - bottom center with margin
            VeneerAnchor.Register(ElementId, ScreenAnchor.BottomCenter, new Vector2(0, 20));

            float slotSize = 40f;
            float spacing = 2f;
            float padding = 4f;

            float width = slotSize * SLOT_COUNT + spacing * (SLOT_COUNT - 1) + padding * 2;
            float height = slotSize + padding * 2;

            SetSize(width, height);
            AnchorTo(AnchorPreset.BottomCenter, new Vector2(0, 20));

            // Background
            _backgroundImage = gameObject.AddComponent<Image>();
            _backgroundImage.sprite = VeneerTextures.CreatePanelSprite();
            _backgroundImage.type = Image.Type.Sliced;
            _backgroundImage.color = VeneerColors.Background;

            // Border
            var borderGo = CreateUIObject("Border", transform);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            _borderImage = borderGo.AddComponent<Image>();
            var borderTex = VeneerTextures.CreateSlicedBorderTexture(16, VeneerColors.Border, Color.clear, 1);
            _borderImage.sprite = VeneerTextures.CreateSlicedSprite(borderTex, 1);
            _borderImage.type = Image.Type.Sliced;
            _borderImage.raycastTarget = false;

            // Content with horizontal layout
            var content = CreateUIObject("Content", transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(padding, padding);
            contentRect.offsetMax = new Vector2(-padding, -padding);

            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = spacing;

            // Create slots
            _slots = new AbilitySlotUI[SLOT_COUNT];
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slots[i] = AbilitySlotUI.Create(content.transform, i, slotSize);
            }

            // Add mover for edit mode dragging
            var mover = gameObject.AddComponent<VeneerMover>();
            mover.ElementId = ElementId;

            // Apply saved position if any
            var savedData = VeneerAnchor.GetAnchorData(ElementId);
            if (savedData != null)
            {
                VeneerAnchor.ApplyAnchor(RectTransform, savedData.Anchor, savedData.Offset);
            }

            Plugin.Log.LogInfo("Viking AbilityBarUI created as HUD element");
        }

        private void Update()
        {
            // Always stay visible - parent (VeneerHud) handles overall HUD visibility
            // Just update cooldowns when player is available
            if (Player.m_localPlayer != null)
            {
                UpdateCooldowns();
            }
        }

        private void OnEnable()
        {
            // Refresh when enabled
            Refresh();
        }

        /// <summary>
        /// Update cooldown overlays from Prime.
        /// </summary>
        private void UpdateCooldowns()
        {
            if (Player.m_localPlayer == null) return;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                // Get cooldown progress from AbilityBar (which queries Prime)
                float progress = AbilityBar.GetCooldownProgress(Player.m_localPlayer, i);

                // SetCooldown expects 0 = ready, 1 = full cooldown
                // GetCooldownProgress returns 0 = start of cooldown, 1 = ready
                // So we need to invert it
                float cooldownFill = 1f - progress;
                _slots[i].SetCooldown(cooldownFill);
            }
        }

        /// <summary>
        /// Refresh the ability bar.
        /// </summary>
        public void Refresh()
        {
            if (Player.m_localPlayer == null) return;

            var data = VikingDataStore.Get(Player.m_localPlayer);
            if (data == null) return;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                string abilityId = null;
                data.AbilitySlots?.TryGetValue(i, out abilityId);
                _slots[i].SetAbility(abilityId);
            }
        }
    }

    /// <summary>
    /// UI for a single ability slot.
    /// </summary>
    public class AbilitySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private int _slotIndex;
        private Image _background;
        private Image _iconImage;
        private Image _cooldownOverlay;
        private Text _keyText;
        private Text _abilityText;
        private Text _cooldownText;
        private string _abilityId;
        private bool _isHovering;

        /// <summary>
        /// Create an ability slot.
        /// </summary>
        public static AbilitySlotUI Create(Transform parent, int slotIndex, float size)
        {
            var go = new GameObject($"Slot_{slotIndex}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;

            var slot = go.AddComponent<AbilitySlotUI>();
            slot._slotIndex = slotIndex;
            slot.Initialize(size);

            return slot;
        }

        private void Initialize(float size)
        {
            // Background
            _background = gameObject.AddComponent<Image>();
            _background.sprite = VeneerTextures.CreateSlotSprite();
            _background.type = Image.Type.Sliced;
            _background.color = VeneerColors.SlotEmpty;

            // Ability icon
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(2, 2);
            iconRect.offsetMax = new Vector2(-2, -2);

            _iconImage = iconGo.AddComponent<Image>();
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget = false;
            _iconImage.enabled = false;

            // Key number (top-left corner)
            var keyGo = new GameObject("Key", typeof(RectTransform));
            keyGo.transform.SetParent(transform, false);
            var keyRect = keyGo.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0, 1);
            keyRect.anchorMax = new Vector2(0, 1);
            keyRect.pivot = new Vector2(0, 1);
            keyRect.anchoredPosition = new Vector2(2, -2);
            keyRect.sizeDelta = new Vector2(12, 12);

            _keyText = keyGo.AddComponent<Text>();
            _keyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _keyText.fontSize = VeneerConfig.GetScaledFontSize(9);
            _keyText.color = VeneerColors.TextGold;
            _keyText.alignment = TextAnchor.UpperLeft;
            _keyText.text = (_slotIndex + 1).ToString();
            _keyText.raycastTarget = false;

            var keyOutline = keyGo.AddComponent<Outline>();
            keyOutline.effectColor = Color.black;
            keyOutline.effectDistance = new Vector2(1, -1);

            // Ability name fallback (shown when no icon)
            var abilityGo = new GameObject("AbilityName", typeof(RectTransform));
            abilityGo.transform.SetParent(transform, false);
            var abilityRect = abilityGo.GetComponent<RectTransform>();
            abilityRect.anchorMin = new Vector2(0.05f, 0.05f);
            abilityRect.anchorMax = new Vector2(0.95f, 0.7f);
            abilityRect.offsetMin = Vector2.zero;
            abilityRect.offsetMax = Vector2.zero;

            _abilityText = abilityGo.AddComponent<Text>();
            _abilityText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _abilityText.fontSize = VeneerConfig.GetScaledFontSize(9);
            _abilityText.color = VeneerColors.Text;
            _abilityText.alignment = TextAnchor.MiddleCenter;
            _abilityText.raycastTarget = false;

            var abilityOutline = abilityGo.AddComponent<Outline>();
            abilityOutline.effectColor = Color.black;
            abilityOutline.effectDistance = new Vector2(1, -1);

            // Cooldown overlay (radial fill)
            var cooldownGo = new GameObject("Cooldown", typeof(RectTransform));
            cooldownGo.transform.SetParent(transform, false);
            var cooldownRect = cooldownGo.GetComponent<RectTransform>();
            cooldownRect.anchorMin = Vector2.zero;
            cooldownRect.anchorMax = Vector2.one;
            cooldownRect.offsetMin = Vector2.zero;
            cooldownRect.offsetMax = Vector2.zero;

            _cooldownOverlay = cooldownGo.AddComponent<Image>();
            _cooldownOverlay.color = new Color(0, 0, 0, 0);
            _cooldownOverlay.raycastTarget = false;
            _cooldownOverlay.type = Image.Type.Filled;
            _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            _cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            _cooldownOverlay.fillClockwise = true;

            // Cooldown timer text (center)
            var cdTextGo = new GameObject("CooldownText", typeof(RectTransform));
            cdTextGo.transform.SetParent(transform, false);
            var cdTextRect = cdTextGo.GetComponent<RectTransform>();
            cdTextRect.anchorMin = Vector2.zero;
            cdTextRect.anchorMax = Vector2.one;
            cdTextRect.offsetMin = Vector2.zero;
            cdTextRect.offsetMax = Vector2.zero;

            _cooldownText = cdTextGo.AddComponent<Text>();
            _cooldownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _cooldownText.fontSize = VeneerConfig.GetScaledFontSize(14);
            _cooldownText.color = Color.white;
            _cooldownText.alignment = TextAnchor.MiddleCenter;
            _cooldownText.raycastTarget = false;

            var cdOutline = cdTextGo.AddComponent<Outline>();
            cdOutline.effectColor = Color.black;
            cdOutline.effectDistance = new Vector2(1, -1);
        }

        /// <summary>
        /// Set the ability in this slot.
        /// </summary>
        public void SetAbility(string abilityId)
        {
            _abilityId = abilityId;

            if (string.IsNullOrEmpty(abilityId))
            {
                _abilityText.text = "";
                _iconImage.enabled = false;
                _background.color = VeneerColors.SlotEmpty;
            }
            else
            {
                // Try to get icon from Prime
                Sprite icon = GetAbilityIcon(abilityId);
                if (icon != null)
                {
                    _iconImage.sprite = icon;
                    _iconImage.enabled = true;
                    _abilityText.text = "";
                }
                else
                {
                    // Fallback to abbreviated ability name
                    _iconImage.enabled = false;
                    string displayName = abilityId.Length > 4 ? abilityId.Substring(0, 4) : abilityId;
                    _abilityText.text = displayName;
                }

                _background.color = VeneerColors.SlotFilled;
            }
        }

        /// <summary>
        /// Get ability icon from Prime.
        /// </summary>
        private Sprite GetAbilityIcon(string abilityId)
        {
            if (!Plugin.HasPrime) return null;

            try
            {
                var ability = Prime.PrimeAPI.GetAbility(abilityId);
                return ability?.Icon;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Set cooldown progress (0-1, where 0 = ready, 1 = full cooldown).
        /// </summary>
        public void SetCooldown(float progress)
        {
            if (progress <= 0.01f)
            {
                _cooldownOverlay.color = new Color(0, 0, 0, 0);
                _cooldownText.text = "";
            }
            else
            {
                _cooldownOverlay.color = new Color(0, 0, 0, 0.6f);
                _cooldownOverlay.fillAmount = progress;

                // Show remaining cooldown time if significant
                float remaining = AbilityBar.GetRemainingCooldown(Player.m_localPlayer, _slotIndex);
                if (remaining >= 1f)
                {
                    _cooldownText.text = Mathf.CeilToInt(remaining).ToString();
                }
                else if (remaining > 0)
                {
                    _cooldownText.text = remaining.ToString("F1");
                }
                else
                {
                    _cooldownText.text = "";
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;

            if (string.IsNullOrEmpty(_abilityId)) return;

            // Build tooltip from ability data
            var tooltipData = BuildAbilityTooltip(_abilityId);
            if (tooltipData != null)
            {
                VeneerTooltip.Show(tooltipData);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            VeneerTooltip.Hide();
        }

        private void OnDisable()
        {
            if (_isHovering)
            {
                VeneerTooltip.Hide();
                _isHovering = false;
            }
        }

        /// <summary>
        /// Build tooltip data for an ability.
        /// </summary>
        public static TooltipData BuildAbilityTooltip(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return null;

            string title = abilityId;
            string body = "";
            string subtitle = "";

            if (Plugin.HasPrime)
            {
                try
                {
                    var ability = Prime.PrimeAPI.GetAbility(abilityId);
                    if (ability != null)
                    {
                        title = ability.DisplayName ?? abilityId;
                        subtitle = ability.TargetType.ToString();

                        var lines = new System.Collections.Generic.List<string>();

                        if (!string.IsNullOrEmpty(ability.Description))
                        {
                            lines.Add(ability.Description);
                        }

                        if (ability.BaseCooldown > 0)
                        {
                            lines.Add($"Cooldown: {ability.BaseCooldown:F1}s");
                        }

                        // Check resource cost
                        if (ability.Cost != null && ability.Cost.Amount > 0)
                        {
                            string costText = ability.Cost.IsPercentage
                                ? $"{ability.Cost.ResourceType}: {ability.Cost.Amount:F0}%"
                                : $"{ability.Cost.ResourceType}: {ability.Cost.Amount:F0}";
                            lines.Add(costText);
                        }

                        // Show damage if any
                        if (ability.BaseDamage > 0)
                        {
                            lines.Add($"Damage: {ability.BaseDamage:F0} {ability.DamageType}");
                        }

                        body = string.Join("\n", lines);
                    }
                }
                catch
                {
                    // Fall back to just showing ability ID
                }
            }

            return new TooltipData
            {
                Title = title,
                Subtitle = subtitle,
                Body = body
            };
        }
    }
}
