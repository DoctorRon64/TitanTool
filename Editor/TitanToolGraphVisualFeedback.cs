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
        private const string STATUS_BADGE_NAME = "titantool-status-badge";

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

            nodeView.style.backgroundColor = new Color(
                categoryColor.r * 0.18f,
                categoryColor.g * 0.18f,
                categoryColor.b * 0.18f,
                0.34f);

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
