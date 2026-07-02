using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TitanTool.Runtime;
using TitanTool.Runtime.Data;
using TitanTool.Runtime.Nodes.Base;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolGraphVisualFeedback {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string CATEGORY_STRIP_NAME = "titantool-category-strip";
        private const string CATEGORY_GLOW_NAME = "titantool-category-glow";
        private const string ICON_BADGE_NAME = "titantool-icon-badge";
        private const string CATEGORY_BADGE_NAME = "titantool-category-badge";
        private const string STATUS_BADGE_NAME = "titantool-status-badge";
        private const string QUICK_ADD_BAR_NAME = "titantool-quick-add-bar";
        private const string QUICK_ADD_FIELD_NAME = "titantool-quick-add-field";
        private const string SEARCH_POPUP_NAME = "titantool-space-search";
        private const string SEARCH_RESULTS_NAME = "titantool-space-search-results";

        private static double s_nextUpdateTime;
        private static readonly Dictionary<VisualElement, Vector2> s_lastGraphPositions = new();
        private static readonly Dictionary<VisualElement, Vector2> s_lastLocalPositions = new();
        private static readonly Dictionary<VisualElement, SearchPopupState> s_searchPopups = new();
        private static readonly HashSet<VisualElement> s_quickAddAttached = new();

        static TitanToolGraphVisualFeedback() {
            EditorApplication.update += UpdateOpenGraphViews;
        }

        private static void UpdateOpenGraphViews() {
            if (EditorApplication.timeSinceStartup < s_nextUpdateTime)
                return;

            s_nextUpdateTime = EditorApplication.timeSinceStartup + 0.08d;

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (!TryGetBossGraphView(window, out VisualElement graphView, out BossGraph graph))
                    continue;

                EnsureQuickAddBar(graphView);
                RuntimeViewState runtimeState = ResolveRuntimeState(graph);
                ApplyVisuals(graphView, runtimeState);
            }
        }

        private static bool TryGetBossGraphView(EditorWindow window, out VisualElement graphView, out BossGraph graph) {
            graphView = null;
            graph = null;

            if (window == null)
                return false;

            Type windowType = window.GetType();
            if (!windowType.Name.Contains("GraphViewEditorWindow"))
                return false;

            object rawGraphView = windowType.GetProperty("GraphView", FLAGS)?.GetValue(window);
            if (!(rawGraphView is VisualElement view))
                return false;

            object graphModel = GetProperty(rawGraphView, "GraphModel");
            object rawGraph = graphModel != null ? GetProperty(graphModel, "Graph") : null;
            if (!(rawGraph is BossGraph bossGraph))
                return false;

            graphView = view;
            graph = bossGraph;
            return true;
        }

        private static RuntimeViewState ResolveRuntimeState(BossGraph graph) {
            if (!Application.isPlaying || graph == null || string.IsNullOrEmpty(graph.assetPath))
                return RuntimeViewState.None;

            if (!(AssetDatabase.LoadMainAssetAtPath(graph.assetPath) is BossGraphAsset runtimeGraph))
                return RuntimeViewState.None;

            HashSet<BossDirector> directors = BossDebugRegistry.Get(runtimeGraph);
            BossDirector director = directors?.FirstOrDefault(candidate => candidate != null && candidate.context != null);
            if (director?.context == null)
                return RuntimeViewState.None;

            return new RuntimeViewState(runtimeGraph, director.context);
        }

        private static void ApplyVisuals(VisualElement graphView, RuntimeViewState runtimeState) {
            foreach (VisualElement element in Traverse(graphView)) {
                BossGraphNode graphNode = GetGraphNodeFromView(element);
                if (graphNode == null)
                    continue;

                ApplyNodeColor(element, graphNode, runtimeState);
            }
        }

        private static IEnumerable<VisualElement> Traverse(VisualElement root) {
            if (root == null)
                yield break;

            Stack<VisualElement> stack = new();
            stack.Push(root);

            while (stack.Count > 0) {
                VisualElement current = stack.Pop();
                yield return current;

                for (int i = current.childCount - 1; i >= 0; i--) {
                    stack.Push(current[i]);
                }
            }
        }

        private static BossGraphNode GetGraphNodeFromView(VisualElement element) {
            object model = GetProperty(element, "Model");
            object node = model != null ? GetProperty(model, "Node") : null;
            return node as BossGraphNode;
        }

        private static void ApplyNodeColor(VisualElement nodeView, BossGraphNode graphNode, RuntimeViewState runtimeState) {
            Color baseColor = TryGetUserNodeColor(nodeView, out Color userColor)
                ? userColor
                : graphNode.categoryColor;

            EnsureCategoryStrip(nodeView).style.backgroundColor = baseColor;
            EnsureCategoryGlow(nodeView).style.backgroundColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.18f);
            SetIconBadge(nodeView, GetIconBadgeText(graphNode), baseColor, graphNode.tooltip);
            SetCategoryBadge(nodeView, graphNode.category.ToString(), baseColor);

            nodeView.style.backgroundColor = new Color(
                Mathf.Lerp(0.10f, baseColor.r, 0.30f),
                Mathf.Lerp(0.10f, baseColor.g, 0.30f),
                Mathf.Lerp(0.12f, baseColor.b, 0.30f),
                0.94f);

            nodeView.style.borderTopLeftRadius = 7f;
            nodeView.style.borderTopRightRadius = 7f;
            nodeView.style.borderBottomLeftRadius = 7f;
            nodeView.style.borderBottomRightRadius = 7f;

            if (TryGetRuntimeStatus(graphNode, runtimeState, out NodeStatus status, out bool visitedThisTick)) {
                Color statusColor = GetStatusColor(status);
                ApplyBorder(nodeView, statusColor, visitedThisTick ? 3f : 1.5f);
                SetStatusBadge(nodeView, StatusShortName(status), statusColor);
                return;
            }

            ApplyBorder(nodeView, new Color(baseColor.r, baseColor.g, baseColor.b, 0.82f), 1.5f);
            HideStatusBadge(nodeView);
        }

        private static VisualElement EnsureCategoryStrip(VisualElement nodeView) {
            VisualElement strip = nodeView.Q<VisualElement>(CATEGORY_STRIP_NAME);
            if (strip != null)
                return strip;

            strip = new VisualElement { name = CATEGORY_STRIP_NAME };
            strip.pickingMode = PickingMode.Ignore;
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.top = 0f;
            strip.style.bottom = 0f;
            strip.style.width = 5f;
            strip.style.borderTopLeftRadius = 4f;
            strip.style.borderBottomLeftRadius = 4f;
            nodeView.Add(strip);
            return strip;
        }

        private static VisualElement EnsureCategoryGlow(VisualElement nodeView) {
            VisualElement glow = nodeView.Q<VisualElement>(CATEGORY_GLOW_NAME);
            if (glow != null)
                return glow;

            glow = new VisualElement { name = CATEGORY_GLOW_NAME };
            glow.pickingMode = PickingMode.Ignore;
            glow.style.position = Position.Absolute;
            glow.style.left = 0f;
            glow.style.right = 0f;
            glow.style.top = 0f;
            glow.style.height = 24f;
            glow.style.borderTopLeftRadius = 7f;
            glow.style.borderTopRightRadius = 7f;
            nodeView.Insert(0, glow);
            return glow;
        }

        private static Label EnsureCategoryBadge(VisualElement nodeView) {
            Label badge = nodeView.Q<Label>(CATEGORY_BADGE_NAME);
            if (badge != null)
                return badge;

            badge = new Label { name = CATEGORY_BADGE_NAME };
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.left = 10f;
            badge.style.bottom = 5f;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.paddingTop = 1f;
            badge.style.paddingBottom = 1f;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 8f;
            badge.style.color = new Color(0.92f, 0.94f, 0.96f);
            nodeView.Add(badge);
            return badge;
        }

        private static void SetCategoryBadge(VisualElement nodeView, string text, Color color) {
            Label badge = EnsureCategoryBadge(nodeView);
            badge.text = text.ToUpperInvariant();
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.42f);
        }

        private static Label EnsureIconBadge(VisualElement nodeView) {
            Label badge = nodeView.Q<Label>(ICON_BADGE_NAME);
            if (badge != null)
                return badge;

            badge = new Label { name = ICON_BADGE_NAME };
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.left = 10f;
            badge.style.top = 4f;
            badge.style.minWidth = 24f;
            badge.style.height = 18f;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 9f;
            badge.style.color = Color.white;
            nodeView.Add(badge);
            return badge;
        }

        private static void SetIconBadge(VisualElement nodeView, string text, Color color, string tooltip) {
            Label badge = EnsureIconBadge(nodeView);
            badge.text = text;
            badge.tooltip = tooltip;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.64f);
        }

        private static string GetIconBadgeText(BossGraphNode graphNode) {
            return GetIconBadgeText(graphNode.displayName, graphNode.category);
        }

        private static string GetIconBadgeText(GraphNodeRegistration registration) {
            if (registration == null)
                return "ND";

            return GetIconBadgeText(registration.displayName, registration.category);
        }

        private static string GetIconBadgeText(string rawDisplayName, BossGraphNodeCategory category) {
            string displayName = rawDisplayName?.ToLowerInvariant() ?? string.Empty;
            if (displayName.Contains("move"))
                return "MV";
            if (displayName.Contains("shoot"))
                return "SH";
            if (displayName.Contains("spawn"))
                return "SP";
            if (displayName.Contains("wait") || displayName.Contains("delay"))
                return "WT";
            if (displayName.Contains("anim"))
                return "AN";
            if (displayName.Contains("health"))
                return "HP";
            if (displayName.Contains("blackboard"))
                return "BB";
            if (displayName.Contains("random"))
                return "RND";
            if (displayName.Contains("sequence"))
                return "SEQ";
            if (displayName.Contains("selector") || displayName.Contains("branch"))
                return "BR";
            if (displayName.Contains("once"))
                return "1X";
            if (displayName.Contains("cooldown"))
                return "CD";
            if (displayName.Contains("parallel"))
                return "PAR";
            if (displayName.Contains("reroute"))
                return "RT";

            return category switch {
                BossGraphNodeCategory.Flow => "FL",
                BossGraphNodeCategory.Composite => "CP",
                BossGraphNodeCategory.Action => "AC",
                BossGraphNodeCategory.Condition => "IF",
                BossGraphNodeCategory.Decorator => "DC",
                BossGraphNodeCategory.Debug => "DB",
                _ => "UT"
            };
        }

        private static Label EnsureStatusBadge(VisualElement nodeView) {
            Label badge = nodeView.Q<Label>(STATUS_BADGE_NAME);
            if (badge != null)
                return badge;

            badge = new Label { name = STATUS_BADGE_NAME };
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.right = 6f;
            badge.style.top = 4f;
            badge.style.paddingLeft = 6f;
            badge.style.paddingRight = 6f;
            badge.style.paddingTop = 1f;
            badge.style.paddingBottom = 1f;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 9f;
            badge.style.color = Color.white;
            nodeView.Add(badge);
            return badge;
        }

        private static void SetStatusBadge(VisualElement nodeView, string text, Color color) {
            Label badge = EnsureStatusBadge(nodeView);
            badge.text = text;
            badge.style.display = DisplayStyle.Flex;
            badge.style.backgroundColor = color;
        }

        private static void HideStatusBadge(VisualElement nodeView) {
            Label badge = nodeView.Q<Label>(STATUS_BADGE_NAME);
            if (badge != null)
                badge.style.display = DisplayStyle.None;
        }

        private static void ApplyBorder(VisualElement nodeView, Color color, float width) {
            nodeView.style.borderTopColor = color;
            nodeView.style.borderRightColor = color;
            nodeView.style.borderBottomColor = color;
            nodeView.style.borderLeftColor = color;

            nodeView.style.borderTopWidth = width;
            nodeView.style.borderRightWidth = width;
            nodeView.style.borderBottomWidth = width;
            nodeView.style.borderLeftWidth = width;
        }

        private static bool TryGetUserNodeColor(VisualElement nodeView, out Color color) {
            color = default;
            object model = GetProperty(nodeView, "Model");
            if (TryGetUserElementColor(model, out color))
                return true;

            object node = model != null ? GetProperty(model, "Node") : null;
            return TryGetUserElementColor(node, out color);
        }

        private static bool TryGetUserElementColor(object target, out Color color) {
            color = default;
            if (target == null)
                return false;

            if (TryReadElementColor(target, out color))
                return true;

            object elementColor = GetMemberValue(target, "ElementColor") ?? GetMemberValue(target, "m_ElementColor");
            return TryReadElementColor(elementColor, out color);
        }

        private static bool TryReadElementColor(object elementColor, out Color color) {
            color = default;
            if (elementColor == null)
                return false;

            if (!TryReadBool(elementColor, out bool hasUserColor, "HasUserColor", "hasUserColor", "m_HasUserColor") || !hasUserColor)
                return false;

            if (!TryReadColor(elementColor, out color, "Color", "color", "m_Color", "ElementColor", "CustomColor"))
                return false;

            color.a = 1f;
            return true;
        }

        private static bool TryReadBool(object target, out bool value, params string[] memberNames) {
            value = false;
            foreach (string memberName in memberNames) {
                object rawValue = GetMemberValue(target, memberName);
                if (rawValue is bool boolValue) {
                    value = boolValue;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadColor(object target, out Color value, params string[] memberNames) {
            value = default;
            foreach (string memberName in memberNames) {
                object rawValue = GetMemberValue(target, memberName);
                if (rawValue is Color colorValue) {
                    value = colorValue;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRuntimeStatus(BossGraphNode graphNode, RuntimeViewState runtimeState, out NodeStatus status, out bool visitedThisTick) {
            status = NodeStatus.Failure;
            visitedThisTick = false;

            if (!runtimeState.isValid || string.IsNullOrEmpty(graphNode.runtimeGuid))
                return false;

            RuntimeNode runtimeNode = runtimeState.graph.GetNode(graphNode.runtimeGuid);
            if (runtimeNode == null)
                return false;

            visitedThisTick = runtimeState.lastTickPath.Contains(runtimeNode);
            RuntimeNodeDebugData debugData = runtimeState.context.GetDebug(runtimeNode);
            if (!visitedThisTick && !debugData.visited)
                return false;

            status = runtimeState.context.GetStatus(runtimeNode);
            return true;
        }

        private static Color GetStatusColor(NodeStatus status) {
            return status switch {
                NodeStatus.Success => new Color(0.18f, 0.78f, 0.36f),
                NodeStatus.Failure => new Color(0.95f, 0.23f, 0.20f),
                NodeStatus.Running => new Color(1.00f, 0.72f, 0.16f),
                _ => Color.gray
            };
        }

        private static string StatusShortName(NodeStatus status) {
            return status switch {
                NodeStatus.Success => "OK",
                NodeStatus.Failure => "FAIL",
                NodeStatus.Running => "RUN",
                _ => status.ToString().ToUpperInvariant()
            };
        }

        private static object GetProperty(object target, string propertyName) {
            return target?.GetType().GetProperty(propertyName, FLAGS)?.GetValue(target);
        }

        private static object GetMemberValue(object target, string memberName) {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, FLAGS);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(target);

            FieldInfo field = type.GetField(memberName, FLAGS);
            return field?.GetValue(target);
        }

        private static void EnsureQuickAddBar(VisualElement graphView) {
            if (graphView.Q<VisualElement>(QUICK_ADD_BAR_NAME) != null)
                return;

            VisualElement bar = new VisualElement { name = QUICK_ADD_BAR_NAME };
            bar.style.position = Position.Absolute;
            bar.style.left = 12f;
            bar.style.top = 10f;
            bar.style.height = 34f;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 8f;
            bar.style.paddingRight = 8f;
            bar.style.paddingTop = 4f;
            bar.style.paddingBottom = 4f;
            bar.style.borderTopLeftRadius = 6f;
            bar.style.borderTopRightRadius = 6f;
            bar.style.borderBottomLeftRadius = 6f;
            bar.style.borderBottomRightRadius = 6f;
            bar.style.backgroundColor = new Color(0.09f, 0.10f, 0.11f, 0.93f);
            bar.style.borderTopColor = new Color(1f, 1f, 1f, 0.10f);
            bar.style.borderRightColor = new Color(1f, 1f, 1f, 0.10f);
            bar.style.borderBottomColor = new Color(1f, 1f, 1f, 0.10f);
            bar.style.borderLeftColor = new Color(1f, 1f, 1f, 0.10f);
            bar.style.borderTopWidth = 1f;
            bar.style.borderRightWidth = 1f;
            bar.style.borderBottomWidth = 1f;
            bar.style.borderLeftWidth = 1f;

            Label label = new Label("Add");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.80f, 0.84f, 0.90f);
            label.style.marginRight = 6f;
            bar.Add(label);

            TextField field = new TextField { name = QUICK_ADD_FIELD_NAME };
            field.style.width = 150f;
            field.style.height = 22f;
            field.style.marginRight = 6f;
            field.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                if (TryCreateNodeFromQuery(graphView, field.value))
                    field.value = string.Empty;

                evt.StopImmediatePropagation();
            });
            bar.Add(field);

            Button addButton = CreateQuickAddButton("+", new Color(0.30f, 0.58f, 0.95f), () => {
                if (TryCreateNodeFromQuery(graphView, field.value))
                    field.value = string.Empty;
            });
            addButton.style.width = 26f;
            bar.Add(addButton);

            AddShortcutButton(bar, graphView, "Seq", "Sequence");
            AddShortcutButton(bar, graphView, "Wait", "Wait");
            AddShortcutButton(bar, graphView, "Move", "Move");
            AddShortcutButton(bar, graphView, "Shoot", "Shoot");
            AddShortcutButton(bar, graphView, "Spawn", "Spawn");

            graphView.Add(bar);

            if (s_quickAddAttached.Add(graphView)) {
                graphView.RegisterCallback<MouseMoveEvent>(evt => {
                    s_lastLocalPositions[graphView] = evt.localMousePosition;
                    s_lastGraphPositions[graphView] = GetGraphPosition(graphView, evt.localMousePosition);
                }, TrickleDown.TrickleDown);
                graphView.RegisterCallback<KeyDownEvent>(evt => OnGraphKeyDown(graphView, evt), TrickleDown.TrickleDown);
                graphView.RegisterCallback<DetachFromPanelEvent>(_ => {
                    HideSearchPopup(graphView);
                    s_quickAddAttached.Remove(graphView);
                    s_lastGraphPositions.Remove(graphView);
                    s_lastLocalPositions.Remove(graphView);
                });
            }
        }

        private static void OnGraphKeyDown(VisualElement graphView, KeyDownEvent evt) {
            if (evt.keyCode != KeyCode.Space)
                return;

            if (IsTextInputFocused(graphView))
                return;

            ShowSearchPopup(graphView);
            evt.StopImmediatePropagation();
        }

        private static void ShowSearchPopup(VisualElement graphView) {
            HideSearchPopup(graphView);

            Vector2 localPosition = s_lastLocalPositions.TryGetValue(graphView, out Vector2 lastLocalPosition)
                ? lastLocalPosition
                : graphView.layout.center;
            Vector2 graphPosition = s_lastGraphPositions.TryGetValue(graphView, out Vector2 lastGraphPosition)
                ? lastGraphPosition
                : GetGraphPosition(graphView, localPosition);

            VisualElement popup = new VisualElement { name = SEARCH_POPUP_NAME };
            popup.style.position = Position.Absolute;
            popup.style.left = Mathf.Clamp(localPosition.x, 12f, Mathf.Max(12f, graphView.layout.width - 390f));
            popup.style.top = Mathf.Clamp(localPosition.y, 50f, Mathf.Max(50f, graphView.layout.height - 340f));
            popup.style.width = 370f;
            popup.style.maxHeight = 330f;
            popup.style.paddingLeft = 8f;
            popup.style.paddingRight = 8f;
            popup.style.paddingTop = 8f;
            popup.style.paddingBottom = 8f;
            popup.style.borderTopLeftRadius = 7f;
            popup.style.borderTopRightRadius = 7f;
            popup.style.borderBottomLeftRadius = 7f;
            popup.style.borderBottomRightRadius = 7f;
            popup.style.backgroundColor = new Color(0.07f, 0.075f, 0.085f, 0.97f);
            popup.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
            popup.style.borderRightColor = new Color(1f, 1f, 1f, 0.18f);
            popup.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);
            popup.style.borderLeftColor = new Color(1f, 1f, 1f, 0.18f);
            popup.style.borderTopWidth = 1f;
            popup.style.borderRightWidth = 1f;
            popup.style.borderBottomWidth = 1f;
            popup.style.borderLeftWidth = 1f;

            TextField field = new TextField();
            field.style.height = 24f;
            field.style.marginBottom = 7f;
            field.tooltip = "Type a node name, category, or alias.";
            popup.Add(field);

            ScrollView results = new ScrollView(ScrollViewMode.Vertical) { name = SEARCH_RESULTS_NAME };
            results.style.maxHeight = 260f;
            popup.Add(results);

            SearchPopupState state = new SearchPopupState(popup, field, results, graphPosition);
            s_searchPopups[graphView] = state;

            field.RegisterValueChangedCallback(_ => RefreshSearchResults(graphView, state));
            field.RegisterCallback<KeyDownEvent>(evt => OnSearchFieldKeyDown(graphView, state, evt), TrickleDown.TrickleDown);
            popup.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) {
                    HideSearchPopup(graphView);
                    evt.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);

            graphView.Add(popup);
            RefreshSearchResults(graphView, state);
            field.schedule.Execute(() => field.Focus());
        }

        private static void HideSearchPopup(VisualElement graphView) {
            if (!s_searchPopups.TryGetValue(graphView, out SearchPopupState state))
                return;

            state.root.RemoveFromHierarchy();
            s_searchPopups.Remove(graphView);
        }

        private static void OnSearchFieldKeyDown(VisualElement graphView, SearchPopupState state, KeyDownEvent evt) {
            if (evt.keyCode == KeyCode.Escape) {
                HideSearchPopup(graphView);
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.DownArrow) {
                state.selectedIndex = Mathf.Min(state.selectedIndex + 1, Mathf.Max(0, state.matches.Count - 1));
                UpdateSearchSelection(state);
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.UpArrow) {
                state.selectedIndex = Mathf.Max(0, state.selectedIndex - 1);
                UpdateSearchSelection(state);
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) {
                if (state.matches.Count > 0) {
                    CreateNodeFromSearch(graphView, state, state.matches[state.selectedIndex]);
                    evt.StopImmediatePropagation();
                }
            }
        }

        private static void RefreshSearchResults(VisualElement graphView, SearchPopupState state) {
            state.matches = GetSearchMatches(state.field.value).Take(9).ToList();
            state.selectedIndex = Mathf.Clamp(state.selectedIndex, 0, Mathf.Max(0, state.matches.Count - 1));
            state.resultButtons.Clear();
            state.results.Clear();

            if (state.matches.Count == 0) {
                Label empty = new Label("No nodes found");
                empty.style.height = 24f;
                empty.style.color = new Color(0.72f, 0.74f, 0.78f);
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                state.results.Add(empty);
                return;
            }

            for (int index = 0; index < state.matches.Count; index++) {
                GraphNodeRegistration registration = state.matches[index];
                Button row = CreateSearchResultButton(registration, () => CreateNodeFromSearch(graphView, state, registration));
                state.resultButtons.Add(row);
                state.results.Add(row);
            }

            UpdateSearchSelection(state);
        }

        private static Button CreateSearchResultButton(GraphNodeRegistration registration, Action clicked) {
            Color color = registration?.color ?? new Color(0.38f, 0.42f, 0.48f);
            string icon = GetIconBadgeText(registration);
            string category = registration?.category.ToString().ToUpperInvariant() ?? "NODE";
            Button button = new Button(clicked) {
                text = $"{icon}  {registration?.displayName ?? "Node"}    {category}"
            };

            button.style.height = 28f;
            button.style.marginBottom = 3f;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.borderTopLeftRadius = 4f;
            button.style.borderTopRightRadius = 4f;
            button.style.borderBottomLeftRadius = 4f;
            button.style.borderBottomRightRadius = 4f;
            button.style.backgroundColor = new Color(color.r, color.g, color.b, 0.24f);
            button.style.color = new Color(0.92f, 0.94f, 0.97f);
            button.tooltip = registration?.tooltip;
            return button;
        }

        private static void UpdateSearchSelection(SearchPopupState state) {
            for (int index = 0; index < state.resultButtons.Count; index++) {
                Button button = state.resultButtons[index];
                GraphNodeRegistration registration = state.matches[index];
                Color color = registration?.color ?? new Color(0.38f, 0.42f, 0.48f);
                bool selected = index == state.selectedIndex;
                button.style.backgroundColor = selected
                    ? new Color(color.r, color.g, color.b, 0.62f)
                    : new Color(color.r, color.g, color.b, 0.24f);
                button.style.borderTopColor = selected ? new Color(1f, 1f, 1f, 0.42f) : Color.clear;
                button.style.borderRightColor = selected ? new Color(1f, 1f, 1f, 0.42f) : Color.clear;
                button.style.borderBottomColor = selected ? new Color(1f, 1f, 1f, 0.42f) : Color.clear;
                button.style.borderLeftColor = selected ? new Color(1f, 1f, 1f, 0.42f) : Color.clear;
                button.style.borderTopWidth = selected ? 1f : 0f;
                button.style.borderRightWidth = selected ? 1f : 0f;
                button.style.borderBottomWidth = selected ? 1f : 0f;
                button.style.borderLeftWidth = selected ? 1f : 0f;
            }
        }

        private static void CreateNodeFromSearch(VisualElement graphView, SearchPopupState state, GraphNodeRegistration registration) {
            s_lastGraphPositions[graphView] = state.graphPosition;
            if (TryCreateNode(graphView, registration?.editorType))
                HideSearchPopup(graphView);
        }

        private static void AddShortcutButton(VisualElement bar, VisualElement graphView, string label, string query) {
            GraphNodeRegistration registration = FindRegistration(query);
            Color color = registration?.color ?? new Color(0.38f, 0.42f, 0.48f);
            bar.Add(CreateQuickAddButton(label, color, () => TryCreateNode(graphView, registration?.editorType)));
        }

        private static Button CreateQuickAddButton(string text, Color color, Action clicked) {
            Button button = new Button(clicked) { text = text };
            button.style.height = 22f;
            button.style.marginLeft = 3f;
            button.style.marginRight = 0f;
            button.style.paddingLeft = 7f;
            button.style.paddingRight = 7f;
            button.style.borderTopLeftRadius = 4f;
            button.style.borderTopRightRadius = 4f;
            button.style.borderBottomLeftRadius = 4f;
            button.style.borderBottomRightRadius = 4f;
            button.style.backgroundColor = new Color(color.r, color.g, color.b, 0.55f);
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        private static bool TryCreateNodeFromQuery(VisualElement graphView, string query) {
            GraphNodeRegistration registration = FindRegistration(query);
            return TryCreateNode(graphView, registration?.editorType);
        }

        private static GraphNodeRegistration FindRegistration(string query) {
            return GetSearchMatches(query).FirstOrDefault();
        }

        private static IReadOnlyList<GraphNodeRegistration> GetSearchMatches(string query) {
            IReadOnlyList<GraphNodeRegistration> registrations = NodeTypeRegistry.GetRegistrations();
            if (registrations.Count == 0)
                return Array.Empty<GraphNodeRegistration>();

            if (string.IsNullOrWhiteSpace(query))
                return registrations
                    .OrderBy(GetDefaultSearchPriority)
                    .ThenBy(registration => registration.displayName)
                    .ToList();

            string normalized = query.Trim().ToLowerInvariant();
            string[] terms = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return registrations
                .Where(registration => terms.All(term => GetSearchBlob(registration).Contains(term)))
                .OrderBy(registration => GetSearchScore(registration, normalized, terms))
                .ThenBy(registration => registration.displayName)
                .ToList();
        }

        private static int GetSearchScore(GraphNodeRegistration registration, string normalizedQuery, IReadOnlyList<string> terms) {
            string displayName = registration.displayName.ToLowerInvariant();
            string editorName = registration.editorType.Name.ToLowerInvariant();
            string aliases = GetSearchAliases(registration);

            if (displayName == normalizedQuery || editorName == normalizedQuery)
                return 0;
            if (displayName.StartsWith(normalizedQuery) || editorName.StartsWith(normalizedQuery))
                return 1;
            if (aliases.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(alias => alias.StartsWith(normalizedQuery)))
                return 2;
            if (terms.All(term => displayName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(part => part.StartsWith(term))))
                return 3;
            return 4;
        }

        private static int GetDefaultSearchPriority(GraphNodeRegistration registration) {
            string displayName = registration.displayName.ToLowerInvariant();
            if (displayName.Contains("sequence"))
                return 0;
            if (displayName.Contains("selector"))
                return 1;
            if (displayName.Contains("wait"))
                return 2;
            if (displayName.Contains("move"))
                return 3;
            if (displayName.Contains("shoot"))
                return 4;
            if (displayName.Contains("spawn"))
                return 5;
            if (displayName.Contains("parallel"))
                return 6;
            if (displayName.Contains("cooldown"))
                return 7;
            if (displayName.Contains("once"))
                return 8;
            return 50 + (int)registration.category;
        }

        private static string GetSearchBlob(GraphNodeRegistration registration) {
            return string.Join(" ", new[] {
                registration.displayName,
                registration.editorType.Name,
                registration.runtimeType.Name,
                registration.menuPath,
                registration.category.ToString(),
                registration.tooltip,
                registration.icon,
                GetIconBadgeText(registration),
                GetSearchAliases(registration)
            }).ToLowerInvariant();
        }

        private static string GetSearchAliases(GraphNodeRegistration registration) {
            string displayName = registration.displayName.ToLowerInvariant();
            List<string> aliases = new List<string>();

            if (displayName.Contains("sequence"))
                aliases.Add("seq then chain flow");
            if (displayName.Contains("selector"))
                aliases.Add("branch if choose condition fallback");
            if (displayName.Contains("wait"))
                aliases.Add("delay pause timer");
            if (displayName.Contains("move"))
                aliases.Add("movement travel go towards away");
            if (displayName.Contains("shoot"))
                aliases.Add("fire bullet projectile pattern attack");
            if (displayName.Contains("spawn"))
                aliases.Add("create instantiate summon prefab");
            if (displayName.Contains("cooldown"))
                aliases.Add("cd gate limit");
            if (displayName.Contains("repeater"))
                aliases.Add("repeat loop for each for");
            if (displayName.Contains("once"))
                aliases.Add("do once single 1x");
            if (displayName.Contains("random"))
                aliases.Add("rng pick multi gate weighted");
            if (displayName.Contains("parallel"))
                aliases.Add("par together simultaneous");
            if (displayName.Contains("blackboard"))
                aliases.Add("variable var key memory value");
            if (displayName.Contains("animation"))
                aliases.Add("anim animator play trigger");
            if (displayName.Contains("health"))
                aliases.Add("hp life damage");
            if (displayName.Contains("reroute"))
                aliases.Add("redirect knot cleanup line wire");

            return string.Join(" ", aliases);
        }

        private static bool TryCreateNode(VisualElement graphView, Type editorType) {
            if (editorType == null)
                return false;

            object graphModel = GetProperty(graphView, "GraphModel");
            if (graphModel == null)
                return false;

            object node;
            try {
                node = Activator.CreateInstance(editorType);
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }

            Vector2 position = s_lastGraphPositions.TryGetValue(graphView, out Vector2 lastPosition)
                ? lastPosition
                : GetGraphPosition(graphView, graphView.layout.center);

            Type graphModelType = graphModel.GetType();
            MethodInfo registerUndo = graphModelType.GetMethod("RegisterUndo", FLAGS, null, new[] { typeof(string) }, null);
            MethodInfo endUndo = graphModelType.GetMethod("EndUndo", FLAGS, null, Type.EmptyTypes, null);
            MethodInfo createNode = FindCreateNodeModelMethod(graphModelType);
            if (createNode == null)
                return false;

            try {
                registerUndo?.Invoke(graphModel, new object[] { $"Create {editorType.Name}" });
                return createNode.Invoke(graphModel, new[] { node, position }) != null;
            } catch (TargetInvocationException exception) {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            } finally {
                endUndo?.Invoke(graphModel, null);
            }
        }

        private static MethodInfo FindCreateNodeModelMethod(Type graphModelType) {
            foreach (MethodInfo method in graphModelType.GetMethods(FLAGS)) {
                if (method.Name != "CreateNodeModel")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(Vector2))
                    return method;
            }

            return null;
        }

        private static Vector2 GetGraphPosition(VisualElement graphView, Vector2 localPosition) {
            object rawContentViewContainer = GetProperty(graphView, "ContentViewContainer");
            if (rawContentViewContainer is VisualElement contentViewContainer) {
                Vector2 worldPosition = graphView.LocalToWorld(localPosition);
                return contentViewContainer.WorldToLocal(worldPosition);
            }

            return localPosition;
        }

        private static bool IsTextInputFocused(VisualElement graphView) {
            VisualElement focusedElement = graphView.panel?.focusController?.focusedElement as VisualElement;
            for (VisualElement current = focusedElement; current != null; current = current.parent) {
                if (current is TextField || current is IMGUIContainer)
                    return true;

                string typeName = current.GetType().Name;
                if (typeName.Contains("TextInput") || typeName.Contains("SearchField"))
                    return true;
            }

            return false;
        }

        private sealed class SearchPopupState {
            public readonly VisualElement root;
            public readonly TextField field;
            public readonly ScrollView results;
            public readonly Vector2 graphPosition;
            public readonly List<Button> resultButtons = new();
            public List<GraphNodeRegistration> matches = new();
            public int selectedIndex;

            public SearchPopupState(VisualElement root, TextField field, ScrollView results, Vector2 graphPosition) {
                this.root = root;
                this.field = field;
                this.results = results;
                this.graphPosition = graphPosition;
            }
        }

        private readonly struct RuntimeViewState {
            public readonly BossGraphAsset graph;
            public readonly NodeContext context;
            public readonly HashSet<RuntimeNode> lastTickPath;
            public bool isValid => graph != null && context != null;

            public static RuntimeViewState None => new(null, null);

            public RuntimeViewState(BossGraphAsset graph, NodeContext context) {
                this.graph = graph;
                this.context = context;
                lastTickPath = context?.lastTickPath != null
                    ? new HashSet<RuntimeNode>(context.lastTickPath)
                    : new HashSet<RuntimeNode>();
            }
        }
    }
}
