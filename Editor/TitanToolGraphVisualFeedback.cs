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

        private static double s_nextUpdateTime;
        private static readonly Dictionary<VisualElement, Vector2> s_lastGraphPositions = new();
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
            Color categoryColor = graphNode.categoryColor;
            EnsureCategoryStrip(nodeView).style.backgroundColor = categoryColor;
            EnsureCategoryGlow(nodeView).style.backgroundColor = new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.14f);
            SetIconBadge(nodeView, GetIconBadgeText(graphNode), categoryColor, graphNode.tooltip);
            SetCategoryBadge(nodeView, graphNode.category.ToString(), categoryColor);

            nodeView.style.backgroundColor = new Color(
                Mathf.Lerp(0.10f, categoryColor.r, 0.16f),
                Mathf.Lerp(0.10f, categoryColor.g, 0.16f),
                Mathf.Lerp(0.12f, categoryColor.b, 0.16f),
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

            ApplyBorder(nodeView, new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.82f), 1.5f);
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
            string displayName = graphNode.displayName?.ToLowerInvariant() ?? string.Empty;
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

            return graphNode.category switch {
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
                graphView.RegisterCallback<MouseMoveEvent>(evt => s_lastGraphPositions[graphView] = GetGraphPosition(graphView, evt.localMousePosition), TrickleDown.TrickleDown);
                graphView.RegisterCallback<DetachFromPanelEvent>(_ => {
                    s_quickAddAttached.Remove(graphView);
                    s_lastGraphPositions.Remove(graphView);
                });
            }
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
            IReadOnlyList<GraphNodeRegistration> registrations = NodeTypeRegistry.GetRegistrations();
            if (registrations.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(query))
                return registrations.FirstOrDefault(registration => registration.displayName == "Sequence") ?? registrations[0];

            string normalized = query.Trim().ToLowerInvariant();
            return registrations.FirstOrDefault(registration => registration.displayName.ToLowerInvariant().StartsWith(normalized))
                   ?? registrations.FirstOrDefault(registration => registration.editorType.Name.ToLowerInvariant().StartsWith(normalized))
                   ?? registrations.FirstOrDefault(registration => registration.menuPath.ToLowerInvariant().Contains(normalized))
                   ?? registrations.FirstOrDefault(registration => registration.displayName.ToLowerInvariant().Contains(normalized))
                   ?? registrations.FirstOrDefault(registration => registration.editorType.Name.ToLowerInvariant().Contains(normalized));
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
