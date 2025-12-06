using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Veneer.Components.Base;
using Veneer.Components.Primitives;
using Veneer.Core;
using Veneer.Theme;
using Viking.Core;
using Viking.Data;
using Viking.Talents;

namespace Viking.UI
{
    /// <summary>
    /// Main talent tree window UI.
    /// </summary>
    public class TalentTreeWindow : MonoBehaviour
    {
        private VeneerFrame _frame;
        private GameObject _content;
        private VeneerText _pointsText;
        private VeneerText _levelText;
        private Dictionary<string, TalentNodeUI> _nodeUIs = new();
        private RectTransform _nodesContainer;
        private ScrollRect _scrollRect;

        // Panning state
        private bool _isPanning;
        private Vector2 _lastMousePosition;

        private const float NODE_SIZE = 40f;
        private const float POSITION_SCALE = 2.8f; // Increased from 1.5 for better spacing
        private const float PAN_SPEED = 1.0f;
        private const float ZOOM_SPEED = 0.1f;
        private const float MIN_ZOOM = 0.3f;
        private const float MAX_ZOOM = 2.5f;
        private const float CONNECTION_THICKNESS = 4f;

        private float _currentZoom = 1.0f;

        /// <summary>
        /// Whether the window is visible.
        /// </summary>
        public bool IsVisible => _frame != null && _frame.gameObject.activeSelf;

        /// <summary>
        /// Create the talent tree window.
        /// </summary>
        public static TalentTreeWindow Create()
        {
            var go = new GameObject("VikingTalentTree");
            var window = go.AddComponent<TalentTreeWindow>();
            window.Initialize();
            return window;
        }

        private void Initialize()
        {
            // Create the main frame using VeneerAPI
            _frame = VeneerAPI.CreateWindow("viking_talents", "Talent Tree", 800, 600);
            _frame.transform.SetParent(VeneerAPI.UIRoot, false);

            // Subscribe to close event
            _frame.OnCloseClicked += Hide;

            // Position in center
            var rect = _frame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            // Create header panel
            CreateHeader();

            // Create content area for nodes
            CreateContent();

            // Hide by default
            _frame.Hide();
        }

        private void CreateHeader()
        {
            var headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(_frame.Content, false);

            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = new Vector2(0, 0);
            headerRect.sizeDelta = new Vector2(0, 40);

            var layout = headerGo.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 20;
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Level text
            _levelText = VeneerText.Create(headerGo.transform, "Level 1");
            _levelText.ApplyStyle(TextStyle.Header);
            var levelLayout = _levelText.gameObject.AddComponent<LayoutElement>();
            levelLayout.preferredWidth = 100;

            // Points text
            _pointsText = VeneerText.Create(headerGo.transform, "Points: 0 available");
            _pointsText.ApplyStyle(TextStyle.Body);
            var pointsLayout = _pointsText.gameObject.AddComponent<LayoutElement>();
            pointsLayout.flexibleWidth = 1;

            // Undo button
            var undoBtn = VeneerButton.Create(headerGo.transform, "Undo", OnUndoClicked);
            var undoLayout = undoBtn.gameObject.AddComponent<LayoutElement>();
            undoLayout.preferredWidth = 80;

            // Reset button
            var resetBtn = VeneerButton.Create(headerGo.transform, "Reset All", OnResetClicked);
            var resetLayout = resetBtn.gameObject.AddComponent<LayoutElement>();
            resetLayout.preferredWidth = 100;
        }

        private void CreateContent()
        {
            _content = new GameObject("TreeContent", typeof(RectTransform));
            _content.transform.SetParent(_frame.Content, false);

            var contentRect = _content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(5, 5);
            contentRect.offsetMax = new Vector2(-5, -45); // Leave room for header

            // Add background for the tree area
            var bgImage = _content.AddComponent<Image>();
            bgImage.sprite = VeneerTextures.CreatePanelSprite();
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.03f, 0.03f, 0.03f, 0.95f); // Darker background for contrast

            // Add scroll rect for panning
            _scrollRect = _content.AddComponent<ScrollRect>();
            _scrollRect.horizontal = true;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.135f;
            _scrollRect.scrollSensitivity = 20f;

            // Viewport with mask
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(_content.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            // Content container for nodes - larger area for panning
            var container = new GameObject("NodesContainer", typeof(RectTransform));
            container.transform.SetParent(viewport.transform, false);
            _nodesContainer = container.GetComponent<RectTransform>();
            _nodesContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _nodesContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _nodesContainer.pivot = new Vector2(0.5f, 0.5f);
            _nodesContainer.sizeDelta = new Vector2(2000, 1800); // Larger container
            _nodesContainer.anchoredPosition = Vector2.zero;

            _scrollRect.viewport = viewportRect;
            _scrollRect.content = _nodesContainer;

            // Create node UIs
            CreateNodes(_nodesContainer);
        }

        private void CreateNodes(Transform parent)
        {
            var allNodes = TalentTreeManager.GetAllNodes().ToList();
            Plugin.Log.LogInfo($"Creating {allNodes.Count} talent nodes");

            // Create connection lines first (so they're behind nodes)
            CreateConnections(parent);

            foreach (var node in allNodes)
            {
                CreateNodeUI(parent, node);
            }

            Plugin.Log.LogInfo($"Created {_nodeUIs.Count} node UIs");
        }

        private void CreateNodeUI(Transform parent, TalentNode node)
        {
            var nodeUI = TalentNodeUI.Create(parent, node);
            nodeUI.OnClicked += OnNodeClicked;
            nodeUI.OnHovered += OnNodeHovered;
            nodeUI.OnUnhovered += OnNodeUnhovered;

            // Position based on node.Position
            var rect = nodeUI.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(
                node.Position.x * POSITION_SCALE,
                node.Position.y * POSITION_SCALE
            );

            _nodeUIs[node.Id] = nodeUI;
        }

        private void CreateConnections(Transform parent)
        {
            foreach (var node in TalentTreeManager.GetAllNodes())
            {
                foreach (var connectedId in node.Connections)
                {
                    var connectedNode = TalentTreeManager.GetNode(connectedId);
                    if (connectedNode == null) continue;

                    // Only draw line once per connection
                    if (string.CompareOrdinal(node.Id, connectedId) > 0) continue;

                    CreateConnectionLine(parent, node, connectedNode);
                }
            }
        }

        private void CreateConnectionLine(Transform parent, TalentNode from, TalentNode to)
        {
            Vector2 fromPos = new Vector2(from.Position.x * POSITION_SCALE, from.Position.y * POSITION_SCALE);
            Vector2 toPos = new Vector2(to.Position.x * POSITION_SCALE, to.Position.y * POSITION_SCALE);
            Vector2 direction = toPos - fromPos;
            float distance = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Create outer glow/border line (wider, darker)
            var glowGo = new GameObject($"LineGlow_{from.Id}_{to.Id}", typeof(RectTransform));
            glowGo.transform.SetParent(parent, false);
            glowGo.transform.SetAsFirstSibling();

            var glowImage = glowGo.AddComponent<Image>();
            glowImage.sprite = VeneerTextures.CreateSprite(VeneerTextures.White);
            glowImage.color = new Color(0.15f, 0.12f, 0.08f, 0.9f); // Dark brown border

            var glowRect = glowGo.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0, 0.5f);
            glowRect.anchoredPosition = fromPos;
            glowRect.sizeDelta = new Vector2(distance, CONNECTION_THICKNESS + 4); // Wider for glow
            glowRect.localRotation = Quaternion.Euler(0, 0, angle);

            // Create main connection line
            var lineGo = new GameObject($"Line_{from.Id}_{to.Id}", typeof(RectTransform));
            lineGo.transform.SetParent(parent, false);
            lineGo.transform.SetAsFirstSibling();

            var lineImage = lineGo.AddComponent<Image>();
            lineImage.sprite = VeneerTextures.CreateSprite(VeneerTextures.White);
            // Viking themed color - golden/bronze gradient feel
            lineImage.color = new Color(0.55f, 0.45f, 0.25f, 0.85f); // Bronze/gold color

            var lineRect = lineGo.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0, 0.5f);
            lineRect.anchoredPosition = fromPos;
            lineRect.sizeDelta = new Vector2(distance, CONNECTION_THICKNESS);
            lineRect.localRotation = Quaternion.Euler(0, 0, angle);

            // Create center highlight (thin bright line in center)
            var highlightGo = new GameObject($"LineHighlight_{from.Id}_{to.Id}", typeof(RectTransform));
            highlightGo.transform.SetParent(parent, false);
            highlightGo.transform.SetAsFirstSibling();

            var highlightImage = highlightGo.AddComponent<Image>();
            highlightImage.sprite = VeneerTextures.CreateSprite(VeneerTextures.White);
            highlightImage.color = new Color(0.9f, 0.8f, 0.5f, 0.4f); // Bright gold center

            var highlightRect = highlightGo.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.pivot = new Vector2(0, 0.5f);
            highlightRect.anchoredPosition = fromPos;
            highlightRect.sizeDelta = new Vector2(distance, 1.5f); // Thin center line
            highlightRect.localRotation = Quaternion.Euler(0, 0, angle);

            // Add rune-like dots along the connection with pulsing animation
            int numDots = Mathf.Max(1, Mathf.FloorToInt(distance / 50f));
            for (int i = 1; i < numDots; i++)
            {
                float t = (float)i / numDots;
                Vector2 dotPos = Vector2.Lerp(fromPos, toPos, t);

                var dotGo = new GameObject($"Dot_{from.Id}_{to.Id}_{i}", typeof(RectTransform));
                dotGo.transform.SetParent(parent, false);

                var dotImage = dotGo.AddComponent<Image>();
                dotImage.sprite = VeneerTextures.CreateSprite(VeneerTextures.White);
                dotImage.color = new Color(0.9f, 0.75f, 0.35f, 0.7f); // Brighter golden dot

                var dotRect = dotGo.GetComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = dotPos;
                dotRect.sizeDelta = new Vector2(7, 7); // Slightly larger dots

                // Add pulsing animation for "living" rune effect
                var pulse = dotGo.AddComponent<ConnectionPulse>();
                pulse.Configure(1.2f + Random.value * 0.6f, 0.35f); // Varied speeds
            }

            // Also pulse the center highlight line subtly
            var linePulse = highlightGo.AddComponent<ConnectionPulse>();
            linePulse.Configure(0.8f, 0.2f);
        }

        private void Update()
        {
            if (!IsVisible) return;

            // Middle mouse button or right mouse button for panning
            if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
            {
                _isPanning = true;
                _lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
            {
                _isPanning = false;
            }

            if (_isPanning && _nodesContainer != null)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _lastMousePosition;
                _nodesContainer.anchoredPosition += delta * PAN_SPEED;
                _lastMousePosition = Input.mousePosition;
            }

            // Scroll wheel for zooming
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f && _nodesContainer != null)
            {
                _currentZoom = Mathf.Clamp(_currentZoom + scroll * ZOOM_SPEED * 10f, MIN_ZOOM, MAX_ZOOM);
                _nodesContainer.localScale = Vector3.one * _currentZoom;
            }
        }

        /// <summary>
        /// Refresh the UI with current player data.
        /// </summary>
        public void Refresh()
        {
            if (Player.m_localPlayer == null) return;

            var player = Player.m_localPlayer;
            var data = VikingDataStore.Get(player);

            // Update header
            int level = Viking.Core.Viking.GetLevel(player);
            int available = VikingDataStore.GetAvailablePoints(player);
            int spent = VikingDataStore.GetSpentPoints(player);

            _levelText.Content = $"Level {level}";
            _pointsText.Content = $"Points: {available} available, {spent} spent";

            // Update nodes
            foreach (var kvp in _nodeUIs)
            {
                var nodeUI = kvp.Value;
                var node = TalentTreeManager.GetNode(kvp.Key);
                if (node == null) continue;

                int currentRanks = data?.GetNodeRanks(kvp.Key) ?? 0;
                bool isAllocated = currentRanks > 0;
                bool canAllocate = Viking.Core.Viking.CanAllocate(player, kvp.Key);

                nodeUI.UpdateState(currentRanks, node.MaxRanks, isAllocated, canAllocate);
            }
        }

        private void OnNodeClicked(TalentNode node)
        {
            if (Player.m_localPlayer == null) return;

            Plugin.Log.LogInfo($"Node clicked: {node.Id}");

            // Check if player needs to choose starting point first
            if (!Viking.Core.Viking.HasStartingPoint(Player.m_localPlayer))
            {
                // If clicking a start node, choose that starting point
                if (node.Type == TalentNodeType.Start)
                {
                    var startPoint = TalentTreeManager.GetAllStartingPoints()
                        .FirstOrDefault(sp => sp.StartNodeId == node.Id);
                    if (startPoint != null)
                    {
                        Plugin.Log.LogInfo($"Choosing starting point: {startPoint.Id}");
                        Viking.Core.Viking.ChooseStartingPoint(startPoint.Id);
                        Invoke(nameof(Refresh), 0.2f);
                    }
                }
                return;
            }

            // Normal allocation
            if (Viking.Core.Viking.CanAllocate(Player.m_localPlayer, node.Id))
            {
                Plugin.Log.LogInfo($"Allocating node: {node.Id}");
                Viking.Core.Viking.RequestAllocateNode(node.Id);
                Invoke(nameof(Refresh), 0.2f);
            }
            else
            {
                Plugin.Log.LogInfo($"Cannot allocate node: {node.Id}");
            }
        }

        private void OnNodeHovered(TalentNode node)
        {
            string tooltip = $"<color=#FFD700>{node.Name}</color>\n";
            tooltip += $"<color=#AAAAAA>{node.Type}</color>\n\n";
            tooltip += node.Description;

            if (node.MaxRanks > 1)
            {
                int current = Player.m_localPlayer != null
                    ? Viking.Core.Viking.GetNodeRanks(Player.m_localPlayer, node.Id)
                    : 0;
                tooltip += $"\n\nRanks: {current}/{node.MaxRanks}";
            }

            if (node.Modifiers.Count > 0)
            {
                tooltip += "\n\n<color=#88FF88>Modifiers (per rank):</color>";
                foreach (var mod in node.Modifiers)
                {
                    string sign = mod.Value >= 0 ? "+" : "";
                    string suffix = mod.Type == TalentModifierType.Percent ? "%" : "";
                    tooltip += $"\n  {sign}{mod.Value}{suffix} {mod.Stat}";
                }
            }

            if (node.HasAbility)
            {
                tooltip += $"\n\n<color=#8888FF>Grants: {node.GrantsAbility}</color>";
            }

            // Show if can allocate
            if (Player.m_localPlayer != null)
            {
                if (!Viking.Core.Viking.HasStartingPoint(Player.m_localPlayer) && node.Type == TalentNodeType.Start)
                {
                    tooltip += "\n\n<color=#FFFF00>Click to choose this starting point</color>";
                }
                else if (Viking.Core.Viking.CanAllocate(Player.m_localPlayer, node.Id))
                {
                    tooltip += "\n\n<color=#00FF00>Click to allocate</color>";
                }
            }

            VeneerAPI.ShowTooltip(tooltip);
        }

        private void OnNodeUnhovered(TalentNode node)
        {
            VeneerAPI.HideTooltip();
        }

        private void OnUndoClicked()
        {
            if (Player.m_localPlayer == null) return;

            if (Viking.Core.Viking.CanBacktrack(Player.m_localPlayer))
            {
                Viking.Core.Viking.RequestBacktrack();
                Invoke(nameof(Refresh), 0.2f);
            }
        }

        private void OnResetClicked()
        {
            if (Player.m_localPlayer == null) return;

            Viking.Core.Viking.RequestFullReset();
            Invoke(nameof(Refresh), 0.2f);
        }

        /// <summary>
        /// Show the window.
        /// </summary>
        public void Show()
        {
            _frame?.Show();
            Refresh();
            // Center the view
            if (_nodesContainer != null)
            {
                _nodesContainer.anchoredPosition = Vector2.zero;
                _currentZoom = 1.0f;
                _nodesContainer.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Hide the window.
        /// </summary>
        public void Hide()
        {
            _frame?.Hide();
        }

        private void OnDestroy()
        {
            if (_frame != null)
            {
                _frame.OnCloseClicked -= Hide;
            }
        }
    }
}
