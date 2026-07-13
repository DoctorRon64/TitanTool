using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TitanTool.Runtime;
using TitanTool.Runtime.Data;
using TitanTool.Runtime.Nodes.Base;
using UnityEditor;
using Unity.GraphToolkit.Editor;
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
        private const string CHILD_CONTROLS_NAME = "titantool-child-controls";
        private const string CHILD_COUNT_OPTION_NAME = "ChildCount";

        private static double s_nextUpdateTime;

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

                RuntimeViewState runtimeState = ResolveRuntimeState(graph);
                ApplyVisuals(graphView, graph, runtimeState);
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

        private static void ApplyVisuals(VisualElement graphView, BossGraph graph, RuntimeViewState runtimeState) {
            HashSet<BossGraphNode> executableNodes = graph != null
                ? BossGraphValidator.GetExecutableNodes(graph.GetNodes().OfType<BossGraphNode>())
                : new HashSet<BossGraphNode>();

            foreach (VisualElement element in Traverse(graphView)) {
                BossGraphNode graphNode = GetGraphNodeFromView(element);
                if (graphNode != null) {
                    ApplyNodeColor(element, graphNode, executableNodes.Contains(graphNode), runtimeState);
                    continue;
                }

                ApplyWireColor(element, runtimeState);
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

        private static void ApplyWireColor(VisualElement wireView, RuntimeViewState runtimeState) {
            object wireModel = GetProperty(wireView, "Model");
            if (!IsWireModel(wireModel))
                return;

            if (!runtimeState.isValid ||
                !TryGetWireEndpointNodes(wireModel, out BossGraphNode fromNode, out BossGraphNode toNode) ||
                fromNode == null) {
                ResetWireVisual(wireView);
                return;
            }

            RuntimeNode fromRuntimeNode = runtimeState.graph.GetNode(fromNode.runtimeGuid);
            RuntimeNode toRuntimeNode = toNode != null ? runtimeState.graph.GetNode(toNode.runtimeGuid) : null;
            bool activePath = fromRuntimeNode != null &&
                              toRuntimeNode != null &&
                              runtimeState.lastTickEdges.Contains(new RuntimeNodeEdge(fromRuntimeNode, toRuntimeNode));

            bool hasStatus = TryGetRuntimeStatus(fromNode, runtimeState, out NodeStatus status, out bool _);
            if (!hasStatus && !activePath) {
                ResetWireVisual(wireView);
                return;
            }

            Color color = GetStatusColor(activePath ? NodeStatus.Running : status);
            color.a = activePath ? 1f : 0.78f;
            SetWireVisual(wireView, color, activePath ? 4.2f : 2.4f);
        }

        private static bool IsWireModel(object model) {
            return model != null && model.GetType().Name.Contains("WireModel", StringComparison.Ordinal);
        }

        private static bool TryGetWireEndpointNodes(object wireModel, out BossGraphNode fromNode, out BossGraphNode toNode) {
            fromNode = GetBossGraphNodeFromPort(GetProperty(wireModel, "FromPort"));
            toNode = GetBossGraphNodeFromPort(GetProperty(wireModel, "ToPort"));
            return fromNode != null || toNode != null;
        }

        private static BossGraphNode GetBossGraphNodeFromPort(object portModel) {
            object nodeModel = GetProperty(portModel, "NodeModel");
            object node = GetProperty(nodeModel, "Node");
            return node as BossGraphNode;
        }

        private static void SetWireVisual(VisualElement wireView, Color color, float width) {
            object wireControl = GetProperty(wireView, "WireControl");
            if (wireControl == null)
                return;

            Type controlType = wireControl.GetType();
            controlType.GetMethod("SetColor", FLAGS, null, new[] { typeof(Color), typeof(Color) }, null)
                ?.Invoke(wireControl, new object[] { color, color });

            PropertyInfo lineWidthProperty = controlType.GetProperty("LineWidth", FLAGS);
            if (lineWidthProperty?.CanWrite == true)
                lineWidthProperty.SetValue(wireControl, width);

            if (wireControl is VisualElement visualElement)
                visualElement.MarkDirtyRepaint();
        }

        private static void ResetWireVisual(VisualElement wireView) {
            object wireControl = GetProperty(wireView, "WireControl");
            if (wireControl == null)
                return;

            Type controlType = wireControl.GetType();
            controlType.GetMethod("ResetColor", FLAGS, null, Type.EmptyTypes, null)?.Invoke(wireControl, null);
            controlType.GetMethod("ResetLineWidth", FLAGS, null, Type.EmptyTypes, null)?.Invoke(wireControl, null);

            if (wireControl is VisualElement visualElement)
                visualElement.MarkDirtyRepaint();
        }

        private static void ApplyNodeColor(VisualElement nodeView, BossGraphNode graphNode, bool isExecutable, RuntimeViewState runtimeState) {
            Color baseColor = TryGetUserNodeColor(nodeView, out Color userColor)
                ? userColor
                : graphNode.categoryColor;

            nodeView.style.opacity = 1f;
            EnsureCategoryStrip(nodeView).style.backgroundColor = baseColor;
            EnsureCategoryGlow(nodeView).style.backgroundColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.18f);
            SetIconBadge(nodeView, GetIconBadgeText(graphNode), baseColor, graphNode.tooltip);
            SetCategoryBadge(nodeView, graphNode.category.ToString(), baseColor);
            ApplyChildControls(nodeView, graphNode, baseColor);

            nodeView.style.backgroundColor = new Color(
                Mathf.Lerp(0.10f, baseColor.r, 0.30f),
                Mathf.Lerp(0.10f, baseColor.g, 0.30f),
                Mathf.Lerp(0.12f, baseColor.b, 0.30f),
                0.94f);

            nodeView.style.borderTopLeftRadius = 7f;
            nodeView.style.borderTopRightRadius = 7f;
            nodeView.style.borderBottomLeftRadius = 7f;
            nodeView.style.borderBottomRightRadius = 7f;

            if (!isExecutable) {
                ApplyGhostNodeVisual(nodeView);
                return;
            }

            if (TryGetRuntimeStatus(graphNode, runtimeState, out NodeStatus status, out bool visitedThisTick)) {
                Color statusColor = GetStatusColor(status);
                ApplyBorder(nodeView, statusColor, visitedThisTick ? 3f : 1.5f);
                SetStatusBadge(nodeView, StatusShortName(status), statusColor);
                return;
            }

            ApplyBorder(nodeView, new Color(baseColor.r, baseColor.g, baseColor.b, 0.82f), 1.5f);
            HideStatusBadge(nodeView);
        }

        private static void ApplyGhostNodeVisual(VisualElement nodeView) {
            Color ghostColor = new(0.42f, 0.46f, 0.52f, 0.72f);
            nodeView.style.opacity = 0.58f;
            nodeView.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 0.72f);
            EnsureCategoryStrip(nodeView).style.backgroundColor = ghostColor;
            EnsureCategoryGlow(nodeView).style.backgroundColor = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.10f);
            ApplyBorder(nodeView, ghostColor, 1f);
            SetStatusBadge(nodeView, "GHOST", ghostColor);
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

        private static void ApplyChildControls(VisualElement nodeView, BossGraphNode graphNode, Color color) {
            if (!TryGetChildCount(graphNode, out int childCount)) {
                HideChildControls(nodeView);
                return;
            }

            VisualElement optionField = FindChildCountOptionField(nodeView, graphNode);
            if (optionField == null || optionField.parent == null) {
                HideChildControls(nodeView);
                return;
            }

            VisualElement parent = optionField.parent;
            int fieldIndex = parent.IndexOf(optionField);
            VisualElement previousControls = nodeView.Q<VisualElement>(CHILD_CONTROLS_NAME);
            if (previousControls != null && previousControls.parent != parent)
                previousControls.RemoveFromHierarchy();

            VisualElement controls = EnsureChildControls(parent, Mathf.Max(0, fieldIndex));
            optionField.style.display = DisplayStyle.None;
            controls.style.display = DisplayStyle.Flex;
            controls.style.backgroundColor = new Color(color.r, color.g, color.b, 0.46f);

            Button removeButton = controls.Q<Button>("child-remove");
            Button addButton = controls.Q<Button>("child-add");
            Label countLabel = controls.Q<Label>("child-count");

            bool canRemove = childCount > graphNode.minimumChildCount && !IsLastChildPortConnected(graphNode, childCount);
            removeButton.SetEnabled(canRemove);
            removeButton.tooltip = canRemove
                ? "Remove the last empty child slot."
                : childCount <= graphNode.minimumChildCount
                    ? $"This node needs at least {graphNode.minimumChildCount} child slot(s)."
                    : "Cannot remove because the last child slot is still connected.";

            countLabel.text = $"Children {childCount}";
            countLabel.tooltip = $"{childCount} child slots";

            removeButton.userData = new ChildControlBinding(graphNode, -1);
            addButton.userData = new ChildControlBinding(graphNode, 1);
        }

        private static VisualElement FindChildCountOptionField(VisualElement nodeView, BossGraphNode graphNode) {
            object optionPortModel = GetChildCountOptionPortModel(graphNode);
            if (optionPortModel != null) {
                foreach (VisualElement element in Traverse(nodeView)) {
                    if (IsInsideChildControls(element))
                        continue;

                    object model = GetProperty(element, "Model");
                    if (ReferenceEquals(model, optionPortModel))
                        return GetOptionFieldRoot(element, nodeView);
                }
            }

            foreach (VisualElement element in Traverse(nodeView)) {
                if (IsInsideChildControls(element))
                    continue;

                if (element is Label label && string.Equals(label.text, "Children", StringComparison.OrdinalIgnoreCase))
                    return GetOptionFieldRoot(label, nodeView);
            }

            return null;
        }

        private static object GetChildCountOptionPortModel(BossGraphNode graphNode) {
            INodeOption option = graphNode.GetNodeOptionByName(CHILD_COUNT_OPTION_NAME);
            return GetProperty(option, "PortModel");
        }

        private static VisualElement GetOptionFieldRoot(VisualElement element, VisualElement nodeView) {
            VisualElement current = element;
            VisualElement best = element.parent ?? element;

            while (current != null && current.parent != null && current.parent != nodeView) {
                if (LooksLikeOptionField(current))
                    best = current;

                current = current.parent;
            }

            return best;
        }

        private static bool LooksLikeOptionField(VisualElement element) {
            string typeName = element.GetType().Name;
            if (typeName.Contains("Field", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("InlineValue", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("ModelProperty", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (string className in element.GetClasses()) {
                if (className.Contains("field", StringComparison.OrdinalIgnoreCase) ||
                    className.Contains("property", StringComparison.OrdinalIgnoreCase) ||
                    className.Contains("inline-value", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsInsideChildControls(VisualElement element) {
            for (VisualElement current = element; current != null; current = current.parent) {
                if (current.name == CHILD_CONTROLS_NAME)
                    return true;
            }

            return false;
        }

        private static VisualElement EnsureChildControls(VisualElement parent, int index) {
            VisualElement controls = parent.Q<VisualElement>(CHILD_CONTROLS_NAME);
            if (controls != null) {
                if (controls.parent != parent) {
                    controls.RemoveFromHierarchy();
                    parent.Insert(Mathf.Clamp(index, 0, parent.childCount), controls);
                }

                return controls;
            }

            controls = new VisualElement { name = CHILD_CONTROLS_NAME };
            controls.style.position = Position.Relative;
            controls.style.left = StyleKeyword.Auto;
            controls.style.right = StyleKeyword.Auto;
            controls.style.top = StyleKeyword.Auto;
            controls.style.bottom = StyleKeyword.Auto;
            controls.style.height = 22f;
            controls.style.marginLeft = 6f;
            controls.style.marginRight = 6f;
            controls.style.marginTop = 2f;
            controls.style.marginBottom = 2f;
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.justifyContent = Justify.Center;
            controls.style.borderTopLeftRadius = 4f;
            controls.style.borderTopRightRadius = 4f;
            controls.style.borderBottomLeftRadius = 4f;
            controls.style.borderBottomRightRadius = 4f;
            controls.style.paddingLeft = 3f;
            controls.style.paddingRight = 3f;

            Button removeButton = CreateChildControlButton("child-remove", "-");
            Label countLabel = new Label { name = "child-count" };
            countLabel.style.minWidth = 72f;
            countLabel.style.height = 18f;
            countLabel.style.marginLeft = 3f;
            countLabel.style.marginRight = 3f;
            countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            countLabel.style.fontSize = 9f;
            countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            countLabel.style.color = Color.white;
            Button addButton = CreateChildControlButton("child-add", "+");

            controls.Add(removeButton);
            controls.Add(countLabel);
            controls.Add(addButton);
            parent.Insert(Mathf.Clamp(index, 0, parent.childCount), controls);
            return controls;
        }

        private static Button CreateChildControlButton(string name, string text) {
            Button button = new Button { name = name, text = text };
            button.RegisterCallback<ClickEvent>(OnChildControlClicked);
            button.style.width = 18f;
            button.style.height = 16f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 1f;
            button.style.borderTopLeftRadius = 3f;
            button.style.borderTopRightRadius = 3f;
            button.style.borderBottomLeftRadius = 3f;
            button.style.borderBottomRightRadius = 3f;
            button.style.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 0.72f);
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        private static void HideChildControls(VisualElement nodeView) {
            VisualElement controls = nodeView.Q<VisualElement>(CHILD_CONTROLS_NAME);
            if (controls != null)
                controls.style.display = DisplayStyle.None;
        }

        private static void OnChildControlClicked(ClickEvent evt) {
            if (evt.currentTarget is not VisualElement target || target.userData is not ChildControlBinding binding)
                return;

            AdjustChildCount(binding.node, binding.delta);
            evt.StopPropagation();
        }

        private static void AdjustChildCount(BossGraphNode graphNode, int delta) {
            if (graphNode == null || !TryGetChildCount(graphNode, out int childCount))
                return;

            int nextCount = Mathf.Max(graphNode.minimumChildCount, childCount + delta);
            if (nextCount == childCount)
                return;

            if (delta < 0 && IsLastChildPortConnected(graphNode, childCount)) {
                Debug.LogWarning("TitanTool: disconnect the last child slot before removing it.");
                return;
            }

            object graphModel = GetGraphModel(graphNode);
            Type graphModelType = graphModel?.GetType();
            MethodInfo registerUndo = graphModelType?.GetMethod("RegisterUndo", FLAGS, null, new[] { typeof(string) }, null);
            MethodInfo endUndo = graphModelType?.GetMethod("EndUndo", FLAGS, null, Type.EmptyTypes, null);

            try {
                registerUndo?.Invoke(graphModel, new object[] { delta > 0 ? "Add Child Slot" : "Remove Child Slot" });
                if (TrySetChildCount(graphNode, nextCount)) {
                    RedefineNode(graphNode);
                    TitanToolEditorSoundSettings.Play(delta > 0
                        ? TitanToolEditorSoundEvent.ChildIncreased
                        : TitanToolEditorSoundEvent.ChildDecreased);
                }
            } catch (TargetInvocationException exception) {
                Debug.LogException(exception.InnerException ?? exception);
            } catch (Exception exception) {
                Debug.LogException(exception);
            } finally {
                endUndo?.Invoke(graphModel, null);
            }
        }

        private static bool TryGetChildCount(BossGraphNode graphNode, out int childCount) {
            childCount = 0;
            INodeOption option = graphNode.GetNodeOptionByName(CHILD_COUNT_OPTION_NAME);
            if (option == null || option.dataType != typeof(int) || !option.TryGetValue(out childCount))
                return false;

            childCount = Mathf.Max(graphNode.minimumChildCount, childCount);
            return true;
        }

        private static bool TrySetChildCount(BossGraphNode graphNode, int childCount) {
            INodeOption option = graphNode.GetNodeOptionByName(CHILD_COUNT_OPTION_NAME);
            object portModel = GetProperty(option, "PortModel");
            object embeddedValue = GetProperty(portModel, "EmbeddedValue");
            PropertyInfo objectValueProperty = embeddedValue?.GetType().GetProperty("ObjectValue", FLAGS);
            if (objectValueProperty?.CanWrite != true)
                return false;

            objectValueProperty.SetValue(embeddedValue, Mathf.Max(graphNode.minimumChildCount, childCount));
            return true;
        }

        private static bool IsLastChildPortConnected(BossGraphNode graphNode, int childCount) {
            string lastPortName = $"Out{Mathf.Max(0, childCount - 1)}";
            INode node = graphNode;
            for (int index = 0; index < node.outputPortCount; index++) {
                IPort port = node.GetOutputPort(index);
                if (port?.name == lastPortName)
                    return port.isConnected;
            }

            return false;
        }

        private static object GetGraphModel(BossGraphNode graphNode) {
            object implementation = GetMemberValue(graphNode, "m_Implementation");
            return GetProperty(implementation, "GraphModel");
        }

        private static void RedefineNode(BossGraphNode graphNode) {
            object implementation = GetMemberValue(graphNode, "m_Implementation");
            implementation?.GetType().GetMethod("DefineNode", FLAGS, null, Type.EmptyTypes, null)?.Invoke(implementation, null);
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

            for (Type type = target.GetType(); type != null; type = type.BaseType) {
                PropertyInfo property = type.GetProperty(memberName, FLAGS);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(target);

                FieldInfo field = type.GetField(memberName, FLAGS);
                if (field != null)
                    return field.GetValue(target);
            }

            return null;
        }

        private readonly struct ChildControlBinding {
            public readonly BossGraphNode node;
            public readonly int delta;

            public ChildControlBinding(BossGraphNode node, int delta) {
                this.node = node;
                this.delta = delta;
            }
        }

        private readonly struct RuntimeViewState {
            public readonly BossGraphAsset graph;
            public readonly NodeContext context;
            public readonly HashSet<RuntimeNode> lastTickPath;
            public readonly HashSet<RuntimeNodeEdge> lastTickEdges;
            public bool isValid => graph != null && context != null;

            public static RuntimeViewState None => new(null, null);

            public RuntimeViewState(BossGraphAsset graph, NodeContext context) {
                this.graph = graph;
                this.context = context;
                lastTickPath = context?.lastTickPath != null
                    ? new HashSet<RuntimeNode>(context.lastTickPath)
                    : new HashSet<RuntimeNode>();
                lastTickEdges = context?.lastTickEdges != null
                    ? new HashSet<RuntimeNodeEdge>(context.lastTickEdges)
                    : new HashSet<RuntimeNodeEdge>();
            }
        }
    }
}
