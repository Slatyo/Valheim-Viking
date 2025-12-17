using System.Collections.Generic;
using System.Reflection;
using Prime;
using Prime.Modifiers;
using UnityEngine;
using UnityEngine.UI;
using Veneer.Components.Base;
using Veneer.Components.Composite;
using Veneer.Components.Primitives;
using Veneer.Components.Specialized;
using Veneer.Core;
using Veneer.Theme;
using Viking.Core;
using Viking.Data;

namespace Viking.UI
{
    /// <summary>
    /// Character window showing equipment and stats.
    /// Replaces the equipment section of the inventory.
    /// </summary>
    public class CharacterWindow : MonoBehaviour
    {
        private VeneerFrame _frame;
        private VeneerTabBar _tabBar;
        private GameObject _gearTab;
        private GameObject _statsTab;
        private GameObject _abilitiesTab;

        // Equipment slots
        private Dictionary<string, VeneerItemSlot> _equipmentSlots = new();
        private Player _player;

        // Ability slots for drag-drop assignment
        private List<AbilitySlotEntry> _abilityEntries = new();

        // Slot size
        private const float SLOT_SIZE = 52f;
        private const float WINDOW_WIDTH = 400f;
        private const float WINDOW_HEIGHT = 520f;

        // Stats text references for updating
        private Dictionary<string, VeneerText> _statTexts = new();

        /// <summary>
        /// Whether the window is visible.
        /// </summary>
        public bool IsVisible => _frame != null && _frame.gameObject.activeSelf;

        /// <summary>
        /// Create the character window.
        /// </summary>
        public static CharacterWindow Create()
        {
            var go = new GameObject("VikingCharacterWindow");
            var window = go.AddComponent<CharacterWindow>();
            window.Initialize();
            return window;
        }

        private void Initialize()
        {
            // Create the main frame
            _frame = VeneerAPI.CreateWindow("viking_character", "Character", WINDOW_WIDTH, WINDOW_HEIGHT);
            _frame.transform.SetParent(VeneerAPI.UIRoot, false);

            // Subscribe to close event
            _frame.OnCloseClicked += Hide;

            // Register with VeneerWindowManager for proper visibility tracking
            _frame.RegisterWithManager();

            // Position in center-left
            var rect = _frame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-200, 0);

            // Create tab bar
            CreateTabBar();

            // Create tab contents
            CreateGearTab();
            CreateStatsTab();
            CreateAbilitiesTab();

            // Select gear tab by default
            _tabBar.SelectTab("gear");

            // Hide by default
            _frame.Hide();
        }

        private void CreateTabBar()
        {
            _tabBar = VeneerTabBar.Create(_frame.Content, 32f);

            var tabRect = _tabBar.RectTransform;
            tabRect.anchorMin = new Vector2(0, 1);
            tabRect.anchorMax = new Vector2(1, 1);
            tabRect.pivot = new Vector2(0.5f, 1);
            tabRect.anchoredPosition = new Vector2(0, 0);
            tabRect.sizeDelta = new Vector2(0, 32);

            _tabBar.AddTabs(
                ("gear", "Gear", 80),
                ("stats", "Stats", 80),
                ("abilities", "Abilities", 100)
            );

            _tabBar.OnTabSelected += OnTabSelected;
        }

        private void OnTabSelected(string tabKey)
        {
            _gearTab.SetActive(tabKey == "gear");
            _statsTab.SetActive(tabKey == "stats");
            _abilitiesTab?.SetActive(tabKey == "abilities");

            // Refresh abilities tab when selected
            if (tabKey == "abilities")
            {
                RefreshAbilitiesTab();
            }
        }

        private void CreateGearTab()
        {
            _gearTab = new GameObject("GearTab", typeof(RectTransform));
            _gearTab.transform.SetParent(_frame.Content, false);

            var gearRect = _gearTab.GetComponent<RectTransform>();
            gearRect.anchorMin = Vector2.zero;
            gearRect.anchorMax = Vector2.one;
            gearRect.offsetMin = new Vector2(10, 10);
            gearRect.offsetMax = new Vector2(-10, -42); // Leave room for tabs

            // Create character silhouette background
            CreateCharacterSilhouette(_gearTab.transform);

            // Create equipment slots around the silhouette
            CreateEquipmentSlots(_gearTab.transform);

            // Create player name and level text
            CreatePlayerInfo(_gearTab.transform);
        }

        private void CreateCharacterSilhouette(Transform parent)
        {
            // Create a simple silhouette panel
            var silhouette = VeneerPanel.Create(parent, "Silhouette", 180, 300);
            silhouette.BackgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.8f);
            silhouette.ShowBorder = true;
            silhouette.BorderColor = VeneerColors.BorderLight;

            var silRect = silhouette.RectTransform;
            silRect.anchorMin = new Vector2(0.5f, 0.5f);
            silRect.anchorMax = new Vector2(0.5f, 0.5f);
            silRect.pivot = new Vector2(0.5f, 0.5f);
            silRect.anchoredPosition = new Vector2(0, 20);

            // Add simple humanoid shape indicator (head, body, legs)
            CreateSilhouetteShape(silhouette.transform);
        }

        private void CreateSilhouetteShape(Transform parent)
        {
            // Head circle
            var head = CreateSilhouettePart(parent, "Head", new Vector2(0, 100), new Vector2(40, 40));

            // Body
            var body = CreateSilhouettePart(parent, "Body", new Vector2(0, 20), new Vector2(60, 100));

            // Legs
            var leftLeg = CreateSilhouettePart(parent, "LeftLeg", new Vector2(-15, -80), new Vector2(25, 70));
            var rightLeg = CreateSilhouettePart(parent, "RightLeg", new Vector2(15, -80), new Vector2(25, 70));

            // Arms
            var leftArm = CreateSilhouettePart(parent, "LeftArm", new Vector2(-45, 30), new Vector2(20, 70));
            var rightArm = CreateSilhouettePart(parent, "RightArm", new Vector2(45, 30), new Vector2(20, 70));
        }

        private GameObject CreateSilhouettePart(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var part = new GameObject(name, typeof(RectTransform), typeof(Image));
            part.transform.SetParent(parent, false);

            var rect = part.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var img = part.GetComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

            return part;
        }

        private void CreateEquipmentSlots(Transform parent)
        {
            // Layout equipment slots around the silhouette
            // Positions relative to center of gear tab

            // Head - top center
            CreateEquipmentSlot(parent, "Head", new Vector2(0, 180));

            // Cape - top left
            CreateEquipmentSlot(parent, "Cape", new Vector2(-120, 130));

            // Ammo - top right
            CreateEquipmentSlot(parent, "Ammo", new Vector2(120, 130));

            // Weapon - left side
            CreateEquipmentSlot(parent, "Weapon", new Vector2(-120, 20));

            // Chest - center (overlaps silhouette slightly)
            CreateEquipmentSlot(parent, "Chest", new Vector2(0, 70));

            // Shield - right side
            CreateEquipmentSlot(parent, "Shield", new Vector2(120, 20));

            // Utility - bottom left
            CreateEquipmentSlot(parent, "Utility", new Vector2(-120, -90));

            // Legs - bottom center
            CreateEquipmentSlot(parent, "Legs", new Vector2(0, -50));
        }

        private void CreateEquipmentSlot(Transform parent, string slotName, Vector2 position)
        {
            var slot = VeneerItemSlot.Create(parent, SLOT_SIZE);
            _equipmentSlots[slotName] = slot;

            var slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = position + new Vector2(0, 20); // Offset for tab bar

            // Add label below slot
            var label = VeneerText.Create(slot.transform, slotName);
            label.ApplyStyle(TextStyle.Caption);
            label.TextColor = VeneerColors.TextMuted;
            label.Alignment = TextAnchor.MiddleCenter;
            label.FontSize = 10;

            var labelRect = label.RectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0);
            labelRect.anchorMax = new Vector2(0.5f, 0);
            labelRect.pivot = new Vector2(0.5f, 1);
            labelRect.anchoredPosition = new Vector2(0, -2);
            labelRect.sizeDelta = new Vector2(60, 14);

            // Set up slot click handling
            slot.OnSlotClick += (s, data) => OnEquipmentSlotClicked(slotName, s, data);
        }

        private void OnEquipmentSlotClicked(string slotName, VeneerItemSlot slot, UnityEngine.EventSystems.PointerEventData data)
        {
            if (_player == null) return;

            var item = slot.Item;
            if (item == null) return;

            // Right click to unequip
            if (data.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            {
                _player.UnequipItem(item, true);
            }
        }

        private void CreatePlayerInfo(Transform parent)
        {
            // Player name and level at top
            var infoPanel = new GameObject("PlayerInfo", typeof(RectTransform));
            infoPanel.transform.SetParent(parent, false);

            var infoRect = infoPanel.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 1);
            infoRect.anchorMax = new Vector2(1, 1);
            infoRect.pivot = new Vector2(0.5f, 1);
            infoRect.anchoredPosition = new Vector2(0, -10);
            infoRect.sizeDelta = new Vector2(0, 40);

            // Level text
            var levelText = VeneerText.Create(infoPanel.transform, "Level 1");
            levelText.ApplyStyle(TextStyle.Header);
            levelText.TextColor = VeneerColors.TextGold;
            levelText.Alignment = TextAnchor.MiddleCenter;
            _statTexts["PlayerLevel"] = levelText;

            var levelRect = levelText.RectTransform;
            levelRect.anchorMin = Vector2.zero;
            levelRect.anchorMax = Vector2.one;
            levelRect.offsetMin = Vector2.zero;
            levelRect.offsetMax = Vector2.zero;
        }

        private void CreateStatsTab()
        {
            _statsTab = new GameObject("StatsTab", typeof(RectTransform));
            _statsTab.transform.SetParent(_frame.Content, false);

            var statsRect = _statsTab.GetComponent<RectTransform>();
            statsRect.anchorMin = Vector2.zero;
            statsRect.anchorMax = Vector2.one;
            statsRect.offsetMin = new Vector2(10, 10);
            statsRect.offsetMax = new Vector2(-10, -42);

            // Add scroll view for stats
            var scrollView = CreateScrollView(_statsTab.transform);
            var content = scrollView.transform.Find("Viewport/Content");

            // Add stat groups
            float yPos = 0;
            yPos = CreateStatGroup(content, "Core Attributes", yPos,
                ("Strength", "Strength"),
                ("Dexterity", "Dexterity"),
                ("Intelligence", "Intelligence"),
                ("Vitality", "Vitality")
            );

            yPos = CreateStatGroup(content, "Combat", yPos,
                ("PhysicalDamage", "Physical Damage"),
                ("FireDamage", "Fire Damage"),
                ("FrostDamage", "Frost Damage"),
                ("LightningDamage", "Lightning Damage"),
                ("PoisonDamage", "Poison Damage"),
                ("SpiritDamage", "Spirit Damage"),
                ("CritChance", "Crit Chance"),
                ("CritDamage", "Crit Damage"),
                ("AttackSpeed", "Attack Speed")
            );

            yPos = CreateStatGroup(content, "Defense", yPos,
                ("Armor", "Armor"),
                ("FireResist", "Fire Resist"),
                ("FrostResist", "Frost Resist"),
                ("LightningResist", "Lightning Resist"),
                ("PoisonResist", "Poison Resist"),
                ("SpiritResist", "Spirit Resist"),
                ("BlockPower", "Block Power")
            );

            yPos = CreateStatGroup(content, "Resources", yPos,
                ("MaxHealth", "Max Health"),
                ("MaxStamina", "Max Stamina"),
                ("MaxEitr", "Max Eitr"),
                ("HealthRegen", "Health Regen"),
                ("StaminaRegen", "Stamina Regen")
            );

            yPos = CreateStatGroup(content, "Movement", yPos,
                ("MoveSpeed", "Move Speed"),
                ("CarryWeight", "Carry Weight")
            );

            // Set content size
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, -yPos + 20);

            _statsTab.SetActive(false);
        }

        private GameObject CreateScrollView(Transform parent)
        {
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(parent, false);

            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            // Invisible image for raycast target (required for scroll input)
            var scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = Color.clear;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 30f;

            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);

            var viewRect = viewport.GetComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;

            // Invisible image for viewport raycast
            var viewImage = viewport.GetComponent<Image>();
            viewImage.color = Color.clear;

            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);

            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 500); // Will be adjusted

            scroll.viewport = viewRect;
            scroll.content = contentRect;

            return scrollGo;
        }

        private float CreateStatGroup(Transform parent, string groupName, float startY, params (string id, string label)[] stats)
        {
            float yPos = startY;
            float groupSpacing = 15f;
            float statHeight = 22f;
            float headerHeight = 28f;

            // Group header
            var header = VeneerText.Create(parent, groupName);
            header.ApplyStyle(TextStyle.Body);
            header.TextColor = VeneerColors.TextGold;
            header.Style = FontStyle.Bold;

            var headerRect = header.RectTransform;
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0, 1);
            headerRect.anchoredPosition = new Vector2(5, yPos);
            headerRect.sizeDelta = new Vector2(-10, headerHeight);

            yPos -= headerHeight;

            // Stats
            foreach (var (id, label) in stats)
            {
                CreateStatRow(parent, id, label, yPos, statHeight);
                yPos -= statHeight;
            }

            yPos -= groupSpacing;
            return yPos;
        }

        private void CreateStatRow(Transform parent, string statId, string label, float yPos, float height)
        {
            var row = new GameObject($"Stat_{statId}", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0, 1);
            rowRect.anchorMax = new Vector2(1, 1);
            rowRect.pivot = new Vector2(0, 1);
            rowRect.anchoredPosition = new Vector2(10, yPos);
            rowRect.sizeDelta = new Vector2(-20, height);

            // Label
            var labelText = VeneerText.Create(row.transform, label);
            labelText.ApplyStyle(TextStyle.Body);
            labelText.TextColor = VeneerColors.TextMuted;
            labelText.Alignment = TextAnchor.MiddleLeft;

            var labelRect = labelText.RectTransform;
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0.6f, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Value
            var valueText = VeneerText.Create(row.transform, "0");
            valueText.ApplyStyle(TextStyle.Body);
            valueText.TextColor = VeneerColors.Text;
            valueText.Alignment = TextAnchor.MiddleRight;
            _statTexts[statId] = valueText;

            var valueRect = valueText.RectTransform;
            valueRect.anchorMin = new Vector2(0.6f, 0);
            valueRect.anchorMax = new Vector2(1, 1);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (!IsVisible || _player == null) return;

            UpdateEquipmentSlots();
            UpdateStats();
        }

        /// <summary>
        /// Refresh the window with current data.
        /// </summary>
        public void Refresh()
        {
            _player = Player.m_localPlayer;
            if (_player == null) return;

            UpdateEquipmentSlots();
            UpdateStats();
            UpdatePlayerInfo();
        }

        private void UpdatePlayerInfo()
        {
            if (_player == null) return;

            int level = Viking.Core.Viking.GetLevel(_player);
            if (_statTexts.TryGetValue("PlayerLevel", out var levelText))
            {
                levelText.Content = $"Level {level}";
            }
        }

        private void UpdateEquipmentSlots()
        {
            if (_player == null) return;

            // Read from EquipmentInventory (separate equipment slots that don't use bag space)
            var equipInv = Core.EquipmentInventory.Instance;
            if (equipInv != null)
            {
                UpdateEquipSlot("Head", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_HELMET));
                UpdateEquipSlot("Chest", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_CHEST));
                UpdateEquipSlot("Legs", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_LEGS));
                UpdateEquipSlot("Cape", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_SHOULDER));
                UpdateEquipSlot("Utility", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_UTILITY));
                UpdateEquipSlot("Ammo", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_AMMO));
                UpdateEquipSlot("Weapon", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_WEAPON_RIGHT));
                UpdateEquipSlot("Shield", equipInv.GetItemInSlot(Core.EquipmentInventory.SLOT_WEAPON_LEFT));
            }
            else
            {
                // Fallback: Read from Humanoid fields if EquipmentInventory not available
                UpdateEquipSlot("Head", GetEquipmentField(_player, "m_helmetItem"));
                UpdateEquipSlot("Chest", GetEquipmentField(_player, "m_chestItem"));
                UpdateEquipSlot("Legs", GetEquipmentField(_player, "m_legItem"));
                UpdateEquipSlot("Cape", GetEquipmentField(_player, "m_shoulderItem"));
                UpdateEquipSlot("Utility", GetEquipmentField(_player, "m_utilityItem"));
                UpdateEquipSlot("Ammo", GetEquipmentField(_player, "m_ammoItem"));

                var visibleWeapon = GetEquipmentField(_player, "m_rightItem");
                var hiddenWeapon = GetEquipmentField(_player, "m_hiddenRightItem");
                UpdateEquipSlot("Weapon", visibleWeapon ?? hiddenWeapon);

                var visibleShield = GetEquipmentField(_player, "m_leftItem");
                var hiddenShield = GetEquipmentField(_player, "m_hiddenLeftItem");
                UpdateEquipSlot("Shield", visibleShield ?? hiddenShield);
            }
        }

        // Cache reflection fields for performance
        private static readonly Dictionary<string, FieldInfo> _equipmentFields = new();

        private static ItemDrop.ItemData GetEquipmentField(Humanoid humanoid, string fieldName)
        {
            if (!_equipmentFields.TryGetValue(fieldName, out var field))
            {
                field = typeof(Humanoid).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _equipmentFields[fieldName] = field;
            }

            return field?.GetValue(humanoid) as ItemDrop.ItemData;
        }

        private void UpdateEquipSlot(string slotName, ItemDrop.ItemData item)
        {
            if (_equipmentSlots.TryGetValue(slotName, out var slot))
            {
                slot.SetItem(item);
            }
        }

        private void UpdateStats()
        {
            if (_player == null) return;

            // Get stats from Prime
            UpdateStatValue("Strength", "Strength");
            UpdateStatValue("Dexterity", "Dexterity");
            UpdateStatValue("Intelligence", "Intelligence");
            UpdateStatValue("Vitality", "Vitality");

            UpdateStatValue("PhysicalDamage", "PhysicalDamage");
            UpdateStatValue("FireDamage", "FireDamage");
            UpdateStatValue("FrostDamage", "FrostDamage");
            UpdateStatValue("LightningDamage", "LightningDamage");
            UpdateStatValue("PoisonDamage", "PoisonDamage");
            UpdateStatValue("SpiritDamage", "SpiritDamage");
            UpdateStatValue("CritChance", "CritChance", isPercent: true);
            UpdateStatValue("CritDamage", "CritDamage", isMultiplier: true);
            UpdateStatValue("AttackSpeed", "AttackSpeed", isMultiplier: true);

            UpdateStatValue("Armor", "Armor");
            UpdateStatValue("FireResist", "FireResist", isPercent: true);
            UpdateStatValue("FrostResist", "FrostResist", isPercent: true);
            UpdateStatValue("LightningResist", "LightningResist", isPercent: true);
            UpdateStatValue("PoisonResist", "PoisonResist", isPercent: true);
            UpdateStatValue("SpiritResist", "SpiritResist", isPercent: true);
            UpdateStatValue("BlockPower", "BlockPower");

            UpdateStatValue("MaxHealth", "MaxHealth");
            UpdateStatValue("MaxStamina", "MaxStamina");
            UpdateStatValue("MaxEitr", "MaxEitr");
            UpdateStatValue("HealthRegen", "HealthRegen");
            UpdateStatValue("StaminaRegen", "StaminaRegen");

            UpdateStatValue("MoveSpeed", "MoveSpeed", isMultiplier: true);
            UpdateStatValue("CarryWeight", "CarryWeight");
        }

        private void UpdateStatValue(string textId, string statId, bool isPercent = false, bool isMultiplier = false)
        {
            if (!_statTexts.TryGetValue(textId, out var text)) return;

            // Prime syncs vanilla bases (food-based health, etc.) so PrimeAPI.Get() returns correct values
            float value = PrimeAPI.Get(_player, statId);

            string formatted;
            if (isPercent)
            {
                formatted = $"{value * 100:F0}%";
            }
            else if (isMultiplier)
            {
                formatted = $"{value:F2}x";
            }
            else
            {
                formatted = value >= 100 ? $"{value:F0}" : $"{value:F1}";
            }

            text.Content = formatted;

            // Color based on value (green for positive bonuses)
            if (value > 0)
            {
                text.TextColor = VeneerColors.Text;
            }
            else
            {
                text.TextColor = VeneerColors.TextMuted;
            }
        }

        /// <summary>
        /// Show the window.
        /// </summary>
        public void Show()
        {
            _frame?.Show();
            Refresh();
        }

        /// <summary>
        /// Hide the window.
        /// </summary>
        public void Hide()
        {
            // Clean up any orphaned drag visuals
            AbilityDragSource.CleanupAllDragVisuals();
            _frame?.Hide();
        }

        /// <summary>
        /// Toggle the window visibility.
        /// </summary>
        public void Toggle()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        private void CreateAbilitiesTab()
        {
            _abilitiesTab = new GameObject("AbilitiesTab", typeof(RectTransform));
            _abilitiesTab.transform.SetParent(_frame.Content, false);

            var abilitiesRect = _abilitiesTab.GetComponent<RectTransform>();
            abilitiesRect.anchorMin = Vector2.zero;
            abilitiesRect.anchorMax = Vector2.one;
            abilitiesRect.offsetMin = new Vector2(10, 10);
            abilitiesRect.offsetMax = new Vector2(-10, -42);

            // Instructions text at top
            var instructions = VeneerText.Create(_abilitiesTab.transform, "Drag abilities to the action bar (slots 1-8)");
            instructions.ApplyStyle(TextStyle.Caption);
            instructions.TextColor = VeneerColors.TextMuted;
            instructions.Alignment = TextAnchor.MiddleCenter;

            var instrRect = instructions.RectTransform;
            instrRect.anchorMin = new Vector2(0, 1);
            instrRect.anchorMax = new Vector2(1, 1);
            instrRect.pivot = new Vector2(0.5f, 1);
            instrRect.anchoredPosition = new Vector2(0, 0);
            instrRect.sizeDelta = new Vector2(0, 24);

            // Action bar preview (8 slots at top)
            CreateActionBarPreview(_abilitiesTab.transform);

            // Scroll view for abilities list
            var scrollView = CreateAbilitiesScrollView(_abilitiesTab.transform);

            _abilitiesTab.SetActive(false);
        }

        private void CreateActionBarPreview(Transform parent)
        {
            var barContainer = new GameObject("ActionBarPreview", typeof(RectTransform));
            barContainer.transform.SetParent(parent, false);

            var barRect = barContainer.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(0.5f, 1);
            barRect.anchoredPosition = new Vector2(0, -30);
            barRect.sizeDelta = new Vector2(0, 50); // 38px slot + 12px padding

            // Background panel
            var bgPanel = VeneerPanel.Create(barContainer.transform, "Background", 0, 0);
            bgPanel.BackgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            bgPanel.ShowBorder = true;
            bgPanel.BorderColor = VeneerColors.Border;

            var bgRect = bgPanel.RectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Horizontal layout for slots
            var layout = barContainer.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Create 8 action bar slots
            // Available width = 380px (window 400 - 20 for tab padding)
            // 8 slots × 38px + 7 × 2px spacing + 12px padding = 304 + 14 + 12 = 330px (fits)
            float slotSize = 38f;
            for (int i = 0; i < 8; i++)
            {
                CreateActionBarSlot(barContainer.transform, i, slotSize);
            }
        }

        private void CreateActionBarSlot(Transform parent, int index, float size)
        {
            var slotGo = new GameObject($"ActionSlot_{index}", typeof(RectTransform));
            slotGo.transform.SetParent(parent, false);

            var slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(size, size);

            var layoutElement = slotGo.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = size;
            layoutElement.preferredHeight = size;

            // Background
            var bg = slotGo.AddComponent<Image>();
            bg.sprite = VeneerTextures.CreateSlotSprite();
            bg.type = Image.Type.Sliced;
            bg.color = VeneerColors.SlotEmpty;

            // Icon
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(2, 2);
            iconRect.offsetMax = new Vector2(-2, -2);

            var iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = false;

            // Key number
            var keyText = VeneerText.Create(slotGo.transform, (index + 1).ToString());
            keyText.ApplyStyle(TextStyle.Caption);
            keyText.FontSize = 9;
            keyText.TextColor = VeneerColors.TextGold;

            var keyRect = keyText.RectTransform;
            keyRect.anchorMin = new Vector2(0, 1);
            keyRect.anchorMax = new Vector2(0, 1);
            keyRect.pivot = new Vector2(0, 1);
            keyRect.anchoredPosition = new Vector2(2, -2);
            keyRect.sizeDelta = new Vector2(12, 12);

            var keyOutline = keyText.gameObject.AddComponent<Outline>();
            keyOutline.effectColor = Color.black;
            keyOutline.effectDistance = new Vector2(1, -1);

            // Store reference for updating
            var entry = new ActionBarSlotUI
            {
                Root = slotGo,
                Background = bg,
                Icon = iconImage,
                Index = index
            };

            // Make droppable
            var dropTarget = slotGo.AddComponent<ActionBarDropTarget>();
            dropTarget.SlotIndex = index;
            dropTarget.OnAbilityDropped += OnAbilityDroppedToSlot;
        }

        private void OnAbilityDroppedToSlot(int slotIndex, string abilityId)
        {
            AbilityBar.SetSlot(slotIndex, abilityId);
            RefreshAbilitiesTab();
        }

        private GameObject CreateAbilitiesScrollView(Transform parent)
        {
            var scrollGo = new GameObject("AbilitiesScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(parent, false);

            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(0, 0);
            scrollRect.offsetMax = new Vector2(0, -90); // Leave room for action bar preview (30 offset + 50 height + 10 spacing)

            var scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = Color.clear;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);

            var viewRect = viewport.GetComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;

            var viewImage = viewport.GetComponent<Image>();
            viewImage.color = Color.clear;

            // Content
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content.transform.SetParent(viewport.transform, false);

            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.spacing = 4f;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var sizeFitter = content.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewRect;
            scroll.content = contentRect;

            return scrollGo;
        }

        private void RefreshAbilitiesTab()
        {
            if (_abilitiesTab == null || _player == null) return;
            if (!Plugin.HasPrime) return;

            // Clear existing entries
            _abilityEntries.Clear();

            // Find content transform
            var scrollView = _abilitiesTab.transform.Find("AbilitiesScroll");
            if (scrollView == null) return;

            var content = scrollView.Find("Viewport/Content");
            if (content == null) return;

            // Clear existing children
            foreach (Transform child in content)
            {
                Destroy(child.gameObject);
            }

            // Get all granted abilities from Prime
            try
            {
                var abilities = Prime.PrimeAPI.GetGrantedAbilities(_player);
                foreach (var ability in abilities)
                {
                    if (ability.Definition.Category == Prime.Abilities.AbilityCategory.Passive)
                        continue; // Skip passive abilities

                    CreateAbilityEntry(content, ability);
                }

                // If no abilities, show message
                bool hasAbilities = false;
                foreach (var _ in abilities)
                {
                    hasAbilities = true;
                    break;
                }

                if (!hasAbilities)
                {
                    var noAbilitiesText = VeneerText.Create(content, "No abilities learned yet.\nAllocate talent points to learn abilities.");
                    noAbilitiesText.ApplyStyle(TextStyle.Body);
                    noAbilitiesText.TextColor = VeneerColors.TextMuted;
                    noAbilitiesText.Alignment = TextAnchor.MiddleCenter;

                    var textRect = noAbilitiesText.RectTransform;
                    textRect.sizeDelta = new Vector2(0, 60);

                    var layoutElement = noAbilitiesText.gameObject.AddComponent<LayoutElement>();
                    layoutElement.preferredHeight = 60;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Failed to refresh abilities tab: {ex.Message}");
            }

            // Update action bar preview
            UpdateActionBarPreview();
        }

        private void CreateAbilityEntry(Transform parent, Prime.Abilities.AbilityInstance ability)
        {
            var entryGo = new GameObject($"Ability_{ability.Definition.Id}", typeof(RectTransform));
            entryGo.transform.SetParent(parent, false);

            var entryRect = entryGo.GetComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(0, 50);

            var layoutElement = entryGo.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleWidth = 1;

            // Background panel
            var bg = entryGo.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.15f, 0.8f);

            // Make draggable
            var dragSource = entryGo.AddComponent<AbilityDragSource>();
            dragSource.AbilityId = ability.Definition.Id;
            dragSource.AbilityIcon = ability.Definition.Icon;

            // Icon
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(entryGo.transform, false);

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0);
            iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(5, 0);
            iconRect.sizeDelta = new Vector2(40, 40);

            var iconBg = iconGo.AddComponent<Image>();
            iconBg.sprite = VeneerTextures.CreateSlotSprite();
            iconBg.type = Image.Type.Sliced;
            iconBg.color = VeneerColors.SlotFilled;

            if (ability.Definition.Icon != null)
            {
                var iconImage = new GameObject("Sprite", typeof(RectTransform));
                iconImage.transform.SetParent(iconGo.transform, false);

                var imgRect = iconImage.GetComponent<RectTransform>();
                imgRect.anchorMin = Vector2.zero;
                imgRect.anchorMax = Vector2.one;
                imgRect.offsetMin = new Vector2(2, 2);
                imgRect.offsetMax = new Vector2(-2, -2);

                var img = iconImage.AddComponent<Image>();
                img.sprite = ability.Definition.Icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            // Name
            var nameText = VeneerText.Create(entryGo.transform, ability.Definition.DisplayName);
            nameText.ApplyStyle(TextStyle.Body);
            nameText.TextColor = VeneerColors.Text;
            nameText.Alignment = TextAnchor.MiddleLeft;

            var nameRect = nameText.RectTransform;
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0, 0.5f);
            nameRect.anchoredPosition = new Vector2(55, -5);
            nameRect.sizeDelta = new Vector2(-70, 20);

            // Description / info
            string info = GetAbilityInfo(ability);
            var infoText = VeneerText.Create(entryGo.transform, info);
            infoText.ApplyStyle(TextStyle.Caption);
            infoText.TextColor = VeneerColors.TextMuted;
            infoText.Alignment = TextAnchor.MiddleLeft;

            var infoRect = infoText.RectTransform;
            infoRect.anchorMin = new Vector2(0, 0);
            infoRect.anchorMax = new Vector2(1, 0.5f);
            infoRect.pivot = new Vector2(0, 0.5f);
            infoRect.anchoredPosition = new Vector2(55, 5);
            infoRect.sizeDelta = new Vector2(-70, 16);

            var entry = new AbilitySlotEntry
            {
                AbilityId = ability.Definition.Id,
                Root = entryGo
            };
            _abilityEntries.Add(entry);
        }

        private string GetAbilityInfo(Prime.Abilities.AbilityInstance ability)
        {
            var def = ability.Definition;
            var parts = new List<string>();

            if (def.BaseCooldown > 0)
                parts.Add($"CD: {def.BaseCooldown:F0}s");

            if (def.Cost != null && def.Cost.Amount > 0)
                parts.Add($"{def.Cost.ResourceType}: {def.Cost.Amount:F0}");

            if (def.BaseDamage > 0)
                parts.Add($"Dmg: {def.BaseDamage:F0}");

            return parts.Count > 0 ? string.Join(" | ", parts) : "Active ability";
        }

        private void UpdateActionBarPreview()
        {
            if (_abilitiesTab == null || _player == null) return;

            var barContainer = _abilitiesTab.transform.Find("ActionBarPreview");
            if (barContainer == null) return;

            for (int i = 0; i < 8; i++)
            {
                var slotTransform = barContainer.Find($"ActionSlot_{i}");
                if (slotTransform == null) continue;

                var icon = slotTransform.Find("Icon")?.GetComponent<Image>();
                var bg = slotTransform.GetComponent<Image>();
                if (icon == null || bg == null) continue;

                string abilityId = AbilityBar.GetSlot(_player, i);
                if (!string.IsNullOrEmpty(abilityId))
                {
                    var ability = Plugin.HasPrime ? Prime.PrimeAPI.GetAbility(abilityId) : null;
                    if (ability?.Icon != null)
                    {
                        icon.sprite = ability.Icon;
                        icon.enabled = true;
                    }
                    else
                    {
                        icon.enabled = false;
                    }
                    bg.color = VeneerColors.SlotFilled;
                }
                else
                {
                    icon.enabled = false;
                    bg.color = VeneerColors.SlotEmpty;
                }
            }
        }

        private void OnDestroy()
        {
            if (_frame != null)
            {
                _frame.OnCloseClicked -= Hide;
            }
        }
    }

    /// <summary>
    /// Helper class for ability slot entries.
    /// </summary>
    internal class AbilitySlotEntry
    {
        public string AbilityId;
        public GameObject Root;
    }

    /// <summary>
    /// Helper class for action bar slot UI.
    /// </summary>
    internal class ActionBarSlotUI
    {
        public GameObject Root;
        public Image Background;
        public Image Icon;
        public int Index;
    }

    /// <summary>
    /// Drag source for abilities - allows dragging abilities to action bar.
    /// </summary>
    internal class AbilityDragSource : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IEndDragHandler,
        UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public string AbilityId;
        public Sprite AbilityIcon;

        private GameObject _dragVisual;
        private static Canvas _rootCanvas;
        private bool _isHovering;

        // Track all active drag visuals for cleanup
        private static readonly List<GameObject> _activeDragVisuals = new List<GameObject>();

        /// <summary>
        /// Cleanup all drag visuals (call when window closes).
        /// </summary>
        public static void CleanupAllDragVisuals()
        {
            foreach (var visual in _activeDragVisuals)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }
            _activeDragVisuals.Clear();

            // Also hide tooltip
            VeneerTooltip.Hide();
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isHovering = true;

            if (string.IsNullOrEmpty(AbilityId)) return;

            // Use the shared tooltip builder from AbilitySlotUI
            var tooltipData = AbilitySlotUI.BuildAbilityTooltip(AbilityId);
            if (tooltipData != null)
            {
                VeneerTooltip.Show(tooltipData);
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isHovering = false;
            VeneerTooltip.Hide();
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Plugin.Log.LogInfo($"OnBeginDrag: Ability={AbilityId}");

            // Find root canvas (use GUIManager for consistent layering)
            if (_rootCanvas == null)
            {
                var guiFront = Jotunn.Managers.GUIManager.CustomGUIFront;
                if (guiFront != null)
                {
                    _rootCanvas = guiFront.GetComponentInParent<Canvas>();
                }
            }

            if (_rootCanvas == null)
            {
                _rootCanvas = GetComponentInParent<Canvas>();
            }

            if (_rootCanvas == null)
            {
                Plugin.Log.LogError("OnBeginDrag: No root canvas found!");
                return;
            }

            Plugin.Log.LogInfo($"OnBeginDrag: Using canvas {_rootCanvas.name}");

            _dragVisual = new GameObject("AbilityDragVisual", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _dragVisual.transform.SetParent(_rootCanvas.transform, false);
            _dragVisual.transform.SetAsLastSibling(); // Ensure it renders on top

            var rect = _dragVisual.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(40, 40);

            var canvasGroup = _dragVisual.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.9f;

            var image = _dragVisual.GetComponent<Image>();
            if (AbilityIcon != null)
            {
                image.sprite = AbilityIcon;
                image.preserveAspect = true;
            }
            else
            {
                // No icon - show colored placeholder
                image.sprite = VeneerTextures.CreateSlotSprite();
                image.type = Image.Type.Sliced;
                image.color = VeneerColors.SlotFilled;
            }

            // Track for cleanup
            _activeDragVisuals.Add(_dragVisual);

            SetDragPosition(eventData);
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            SetDragPosition(eventData);
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            DestroyDragVisual();
        }

        private void OnDisable()
        {
            // Clean up if component is disabled while dragging
            DestroyDragVisual();

            // Hide tooltip if hovering
            if (_isHovering)
            {
                VeneerTooltip.Hide();
                _isHovering = false;
            }
        }

        private void OnDestroy()
        {
            // Clean up if component is destroyed while dragging
            DestroyDragVisual();
        }

        private void DestroyDragVisual()
        {
            if (_dragVisual != null)
            {
                _activeDragVisuals.Remove(_dragVisual);
                Destroy(_dragVisual);
                _dragVisual = null;
            }
        }

        private void SetDragPosition(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_dragVisual == null || _rootCanvas == null) return;

            // Convert screen position to canvas position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            _dragVisual.GetComponent<RectTransform>().anchoredPosition = localPoint;
        }
    }

    /// <summary>
    /// Drop target for action bar slots.
    /// </summary>
    internal class ActionBarDropTarget : MonoBehaviour, UnityEngine.EventSystems.IDropHandler,
        UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public int SlotIndex;
        public event System.Action<int, string> OnAbilityDropped;

        private Image _image;
        private Color _originalColor;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _originalColor = _image.color;
            }
        }

        public void OnDrop(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Plugin.Log.LogInfo($"OnDrop: SlotIndex={SlotIndex}, pointerDrag={eventData.pointerDrag?.name ?? "null"}");

            var dragSource = eventData.pointerDrag?.GetComponent<AbilityDragSource>();
            if (dragSource != null && !string.IsNullOrEmpty(dragSource.AbilityId))
            {
                Plugin.Log.LogInfo($"OnDrop: Assigning ability {dragSource.AbilityId} to slot {SlotIndex}");
                OnAbilityDropped?.Invoke(SlotIndex, dragSource.AbilityId);
            }

            if (_image != null)
            {
                _image.color = _originalColor;
            }
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (eventData.pointerDrag?.GetComponent<AbilityDragSource>() != null)
            {
                if (_image != null)
                {
                    _image.color = VeneerColors.Accent;
                }
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_image != null)
            {
                _image.color = _originalColor;
            }
        }
    }
}
