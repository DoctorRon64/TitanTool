using System;
using System.Collections.Generic;
using System.Linq;
using TitanTool.Runtime;
using TitanTool.Runtime.Data;
using TitanTool.Runtime.Nodes.Base;
using UnityEditor;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor {
    public class BossGraphDebugWindow : EditorWindow {
        private BossGraphAsset m_graph;
        private BossDirector m_selectedDirector;
        private Vector2 m_scroll;
        private string m_filter = string.Empty;
        private bool m_showOnlyVisited;
        private string m_selectedNodeGuid = string.Empty;

        private GUIStyle m_headerStyle;
        private GUIStyle m_subtleLabelStyle;
        private GUIStyle m_metricValueStyle;
        private GUIStyle m_metricLabelStyle;
        private GUIStyle m_cardStyle;
        private GUIStyle m_nodeRowStyle;
        private GUIStyle m_nodeRowAltStyle;
        private GUIStyle m_columnHeaderStyle;
        private GUIStyle m_statusPillStyle;
        private GUIStyle m_guidStyle;
        private GUIStyle m_monoStyle;
        private GUIStyle m_toolbarButtonStyle;
        private GUIStyle m_toolbarSearchFieldStyle;
        private GUIStyle m_miniButtonStyle;
        private Texture2D m_cardTexture;
        private Texture2D m_rowTexture;
        private Texture2D m_rowAltTexture;

        [MenuItem("Window/TitanTool/Runtime Debugger")]
        private static void Open() {
            OpenWindow();
        }

        internal static void OpenWindow() {
            GetWindow<BossGraphDebugWindow>("TitanTool Debug");
        }

        private void OnEnable() {
            EditorApplication.update += Repaint;
        }

        private void OnDisable() {
            EditorApplication.update -= Repaint;
            DestroyTexture(m_cardTexture);
            DestroyTexture(m_rowTexture);
            DestroyTexture(m_rowAltTexture);
            m_cardTexture = null;
            m_rowTexture = null;
            m_rowAltTexture = null;
            m_headerStyle = null;
        }

        private void OnGUI() {
            EnsureStyles();
            DrawWindowBackground();

            Color previousContentColor = GUI.contentColor;
            GUI.contentColor = Color.white;
            try {
                DrawToolbar();

                if (m_graph == null) {
                    DrawEmptyState("No graph selected", "Assign a BossGraphAsset or use the current selection to inspect a runtime graph.");
                    return;
                }

                BossDirector director = ResolveDirector();
                if (director == null || director.context == null) {
                    DrawEmptyState("Waiting for runtime data", "Enter Play Mode and run a BossDirector using this graph to see live node status.");
                    DrawNodeList(null, new HashSet<RuntimeNode>());
                    return;
                }

                HashSet<RuntimeNode> lastTickPath = director.context.lastTickPath.ToHashSet();
                DrawRuntimeSummary(director, lastTickPath);
                DrawSelectedNodeDetails(director.context, lastTickPath);
                DrawActiveEdges(director.context);
                DrawTraceTimeline(director.context);
                DrawBlackboard(director.context);
                DrawBlackboardChanges(director.context);
                DrawNodeList(director.context, lastTickPath);
            }
            finally {
                GUI.contentColor = previousContentColor;
            }
        }

        private void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                UnityEngine.Object picked = EditorGUILayout.ObjectField(m_graph, typeof(BossGraphAsset), false, GUILayout.MinWidth(220));
                if (picked is BossGraphAsset graph)
                    m_graph = graph;

                if (GUILayout.Button("Use Selection", m_toolbarButtonStyle, GUILayout.Width(100)))
                    UseSelection();

                GUILayout.FlexibleSpace();

                m_showOnlyVisited = GUILayout.Toggle(m_showOnlyVisited, "Visited", m_toolbarButtonStyle, GUILayout.Width(62));

                Rect searchRect = GUILayoutUtility.GetRect(160f, 240f, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(false));
                m_filter = EditorGUI.TextField(searchRect, m_filter, m_toolbarSearchFieldStyle);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(m_filter))) {
                    if (GUILayout.Button("x", m_toolbarButtonStyle, GUILayout.Width(22))) {
                        m_filter = string.Empty;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private void UseSelection() {
            if (Selection.activeObject is BossGraphAsset selectedGraph) {
                m_graph = selectedGraph;
                m_selectedDirector = null;
                return;
            }

            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.TryGetComponent(out BossDirector director)) {
                m_selectedDirector = director;
                m_graph = director.graph;
            }
        }

        private BossDirector ResolveDirector() {
            if (m_selectedDirector != null && m_selectedDirector.graph == m_graph)
                return m_selectedDirector;

            HashSet<BossDirector> directors = BossDebugRegistry.Get(m_graph);
            if (directors == null || directors.Count == 0)
                return null;

            m_selectedDirector = directors.FirstOrDefault(director => director != null);
            return m_selectedDirector;
        }

        private void DrawRuntimeSummary(BossDirector director, HashSet<RuntimeNode> lastTickPath) {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(m_cardStyle, GUILayout.Height(34))) {
                DrawCompactMetric("Runtime", Application.isPlaying ? "Live" : "Editor", Application.isPlaying ? new Color(0.22f, 0.75f, 0.38f) : new Color(0.48f, 0.48f, 0.48f), 92f);
                DrawCompactMetric("Visited", lastTickPath.Count.ToString(), new Color(0.32f, 0.58f, 1f), 82f);
                DrawCompactMetric("Nodes", m_graph.nodes.Count(node => node != null).ToString(), new Color(0.86f, 0.62f, 0.24f), 72f);
                DrawCompactMetric("Director", director.name, new Color(0.65f, 0.52f, 0.95f), 180f);
                GUILayout.FlexibleSpace();
            }

            DrawActivePath(director.context.lastTickPath);
        }

        private void DrawActiveEdges(NodeContext context) {
            if (context == null || context.lastTickEdges.Count == 0)
                return;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                    EditorGUILayout.LabelField("Active Edges", m_headerStyle, GUILayout.Width(86));
                    EditorGUILayout.LabelField($"{context.lastTickEdges.Count} this tick", m_subtleLabelStyle, GUILayout.Width(92));
                    GUILayout.FlexibleSpace();
                }

                foreach (RuntimeNodeEdge edge in context.lastTickEdges.Skip(Math.Max(0, context.lastTickEdges.Count - 8))) {
                    string from = edge.from != null ? edge.from.displayName : "Missing";
                    string to = edge.to != null ? edge.to.displayName : "Missing";
                    NodeStatus status = edge.to != null ? context.GetStatus(edge.to) : NodeStatus.Failure;

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                        DrawStatusPill(StatusShortName(status), GetStatusColor(status, true));
                        EditorGUILayout.LabelField(from, m_metricValueStyle, GUILayout.Width(150));
                        EditorGUILayout.LabelField(">", m_guidStyle, GUILayout.Width(16));
                        EditorGUILayout.LabelField(to, m_subtleLabelStyle);
                    }
                }
            }
        }

        private void DrawTraceTimeline(NodeContext context) {
            if (context == null || context.traceEvents.Count == 0)
                return;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                    EditorGUILayout.LabelField("Timeline", m_headerStyle, GUILayout.Width(70));
                    EditorGUILayout.LabelField($"last {Math.Min(12, context.traceEvents.Count)} events", m_subtleLabelStyle, GUILayout.Width(96));
                    GUILayout.FlexibleSpace();
                }

                foreach (RuntimeNodeTraceEvent trace in context.traceEvents.Skip(Math.Max(0, context.traceEvents.Count - 12)).Reverse()) {
                    RuntimeNode node = trace.node;
                    string indent = new string(' ', Math.Min(trace.depth, 8) * 2);
                    string nodeName = node != null ? node.displayName : "Missing";
                    string reason = string.IsNullOrWhiteSpace(trace.reason) ? string.Empty : $" - {trace.reason}";

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                        EditorGUILayout.LabelField(trace.frame.ToString(), m_guidStyle, GUILayout.Width(48));
                        DrawStatusPill(StatusShortName(trace.status), GetStatusColor(trace.status, true));
                        EditorGUILayout.LabelField($"{indent}{nodeName}", m_metricValueStyle, GUILayout.MinWidth(140));
                        EditorGUILayout.LabelField($"{trace.duration * 1000f:F2} ms{reason}", m_subtleLabelStyle);
                    }
                }
            }
        }

        private void DrawNodeList(NodeContext context, HashSet<RuntimeNode> lastTickPath) {
            List<RuntimeNode> visibleNodes = m_graph.nodes
                .Where(node => node != null)
                .Where(node => MatchesFilter(node) && (!m_showOnlyVisited || lastTickPath.Contains(node)))
                .OrderByDescending(lastTickPath.Contains)
                .ThenBy(node => node.position.y)
                .ThenBy(node => node.position.x)
                .ToList();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Nodes", m_headerStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{visibleNodes.Count} shown", m_subtleLabelStyle, GUILayout.Width(72));
            }

            m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
            if (visibleNodes.Count == 0) {
                DrawEmptyState("No matching nodes", "Adjust the search or show all nodes to inspect this graph.");
            }

            DrawNodeListHeader();

            for (int i = 0; i < visibleNodes.Count; i++) {
                RuntimeNode node = visibleNodes[i];
                DrawNodeRow(node, context, lastTickPath.Contains(node), i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawNodeListHeader() {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                GUILayout.Space(18);
                EditorGUILayout.LabelField("Status", m_columnHeaderStyle, GUILayout.Width(62));
                EditorGUILayout.LabelField("Node", m_columnHeaderStyle, GUILayout.MinWidth(130));
                EditorGUILayout.LabelField("Type", m_columnHeaderStyle, GUILayout.Width(142));
                EditorGUILayout.LabelField("Ticks", m_columnHeaderStyle, GUILayout.Width(42));
                EditorGUILayout.LabelField("Time", m_columnHeaderStyle, GUILayout.Width(64));
                EditorGUILayout.LabelField("Ch", m_columnHeaderStyle, GUILayout.Width(28));
                EditorGUILayout.LabelField("Reason", m_columnHeaderStyle, GUILayout.MinWidth(150));
                EditorGUILayout.LabelField("Guid", m_columnHeaderStyle, GUILayout.MinWidth(80));
                GUILayout.Space(50);
            }
        }

        private void DrawNodeRow(RuntimeNode node, NodeContext context, bool visitedThisTick, int index) {
            NodeStatus status = context?.GetStatus(node) ?? NodeStatus.Failure;
            RuntimeNodeDebugData debug = context?.GetDebug(node);
            Color color = context == null
                ? new Color(0.35f, 0.35f, 0.35f)
                : GetStatusColor(status, visitedThisTick);

            GUIStyle rowStyle = index % 2 == 0 ? m_nodeRowStyle : m_nodeRowAltStyle;
            Rect rect = EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(26));
            bool selected = string.Equals(m_selectedNodeGuid, node.guid, StringComparison.Ordinal);
            if (selected) {
                EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y + 1f, Mathf.Max(1f, rect.width - 8f), Mathf.Max(1f, rect.height - 2f)), new Color(0.22f, 0.35f, 0.58f, 0.28f));
            }

            Rect borderRect = new(rect.x, rect.y + 1f, 4f, Mathf.Max(1f, rect.height - 2f));
            EditorGUI.DrawRect(borderRect, color);

            GUILayout.Space(8);
            DrawStatusPill(visitedThisTick ? StatusShortName(status) : "Idle", color);
            EditorGUILayout.LabelField(node.displayName, selected || visitedThisTick ? m_headerStyle : m_subtleLabelStyle, GUILayout.MinWidth(130));
            EditorGUILayout.LabelField(node.GetType().Name, m_subtleLabelStyle, GUILayout.Width(142));
            EditorGUILayout.LabelField(debug != null ? debug.tickCount.ToString() : "--", m_monoStyle, GUILayout.Width(42));
            EditorGUILayout.LabelField(context != null ? $"{context.GetTiming(node) * 1000f:F2}" : "--", m_monoStyle, GUILayout.Width(64));
            EditorGUILayout.LabelField(node.children.Count(child => child != null).ToString(), m_monoStyle, GUILayout.Width(28));
            EditorGUILayout.LabelField(GetDebugReason(debug), m_subtleLabelStyle, GUILayout.MinWidth(150));
            EditorGUILayout.LabelField(ShortGuid(node.guid), m_guidStyle, GUILayout.MinWidth(80));
            if (GUILayout.Button("Ping", m_miniButtonStyle, GUILayout.Width(44))) {
                    Selection.activeObject = node;
                    EditorGUIUtility.PingObject(node);
            }

            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                m_selectedNodeGuid = node.guid;
                GUI.FocusControl(null);
                Repaint();
                Event.current.Use();
            }
        }

        private void DrawSelectedNodeDetails(NodeContext context, HashSet<RuntimeNode> lastTickPath) {
            RuntimeNode node = ResolveSelectedNode(context, lastTickPath);
            if (node == null)
                return;

            RuntimeNodeDebugData debug = context.GetDebug(node);
            NodeStatus status = context.GetStatus(node);
            bool visitedThisTick = lastTickPath.Contains(node);
            List<RuntimeNode> parents = m_graph.nodes
                .Where(candidate => candidate != null && candidate.children.Contains(node))
                .ToList();
            List<RuntimeNode> activeParents = context.lastTickEdges
                .Where(edge => ReferenceEquals(edge.to, node) && edge.from != null)
                .Select(edge => edge.from)
                .Distinct()
                .ToList();

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(20))) {
                    DrawStatusPill(visitedThisTick ? StatusShortName(status) : "Idle", GetStatusColor(status, visitedThisTick));
                    EditorGUILayout.LabelField(node.displayName, m_headerStyle, GUILayout.MinWidth(150));
                    EditorGUILayout.LabelField(node.GetType().Name, m_subtleLabelStyle, GUILayout.Width(150));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", m_miniButtonStyle, GUILayout.Width(44))) {
                        Selection.activeObject = node;
                        EditorGUIUtility.PingObject(node);
                    }
                }

                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                    DrawDetailMetric("Ticks", debug.tickCount.ToString(), 86f);
                    DrawDetailMetric("Time", $"{context.GetTiming(node) * 1000f:F2} ms", 112f);
                    DrawDetailMetric("Children", node.children.Count(child => child != null).ToString(), 98f);
                    DrawDetailMetric("Guid", ShortGuid(node.guid), 116f);
                    GUILayout.FlexibleSpace();
                }

                DrawDetailLine("Reason", GetDebugReason(debug));
                DrawDetailLine("Parents", parents.Count == 0 ? "None" : string.Join(", ", parents.Select(parent => parent.displayName)));
                DrawDetailLine("Active Parent", activeParents.Count == 0 ? "None this tick" : string.Join(", ", activeParents.Select(parent => parent.displayName)));

                if (node.children.Any(child => child != null)) {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Child Status", m_columnHeaderStyle);
                    foreach (RuntimeNode child in node.children.Where(child => child != null)) {
                        NodeStatus childStatus = context.GetStatus(child);
                        RuntimeNodeDebugData childDebug = context.GetDebug(child);
                        bool childVisited = lastTickPath.Contains(child);
                        using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                            DrawStatusPill(childVisited ? StatusShortName(childStatus) : "Idle", GetStatusColor(childStatus, childVisited));
                            EditorGUILayout.LabelField(child.displayName, m_metricValueStyle, GUILayout.Width(150));
                            EditorGUILayout.LabelField(GetDebugReason(childDebug), m_subtleLabelStyle);
                        }
                    }
                }
            }
        }

        private RuntimeNode ResolveSelectedNode(NodeContext context, HashSet<RuntimeNode> lastTickPath) {
            RuntimeNode selected = !string.IsNullOrEmpty(m_selectedNodeGuid)
                ? m_graph.nodes.FirstOrDefault(node => node != null && node.guid == m_selectedNodeGuid)
                : null;

            if (selected != null)
                return selected;

            RuntimeNode activeNode = lastTickPath.LastOrDefault(node => node != null);
            if (activeNode != null) {
                m_selectedNodeGuid = activeNode.guid;
                return activeNode;
            }

            RuntimeNode tracedNode = context.traceEvents.LastOrDefault(trace => trace.node != null).node;
            if (tracedNode != null) {
                m_selectedNodeGuid = tracedNode.guid;
                return tracedNode;
            }

            return null;
        }

        private void DrawDetailMetric(string label, string value, float width) {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(width))) {
                EditorGUILayout.LabelField(label, m_metricLabelStyle, GUILayout.Width(48));
                EditorGUILayout.LabelField(value, m_metricValueStyle);
            }
        }

        private void DrawDetailLine(string label, string value) {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                EditorGUILayout.LabelField(label, m_metricLabelStyle, GUILayout.Width(86));
                EditorGUILayout.LabelField(value, m_subtleLabelStyle);
            }
        }

        private void DrawCompactMetric(string label, string value, Color accent, float width) {
            Rect rect = GUILayoutUtility.GetRect(width, 22f, GUILayout.Width(width), GUILayout.Height(22f));
            if (Event.current.type == EventType.Repaint) {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 3f, rect.height - 6f), accent);
            }

            GUI.Label(new Rect(rect.x + 8f, rect.y, 48f, rect.height), label, m_metricLabelStyle);
            GUI.Label(new Rect(rect.x + 58f, rect.y, rect.width - 58f, rect.height), value, m_metricValueStyle);
        }

        private void DrawActivePath(IReadOnlyList<RuntimeNode> lastTickPath) {
            using (new EditorGUILayout.HorizontalScope(m_cardStyle, GUILayout.Height(28))) {
                EditorGUILayout.LabelField("Path", m_metricLabelStyle, GUILayout.Width(36));

                if (lastTickPath == null || lastTickPath.Count == 0) {
                    EditorGUILayout.LabelField("No nodes visited on the last tick", m_subtleLabelStyle);
                    return;
                }

                string path = string.Join("  >  ", lastTickPath.Where(node => node != null).Select(node => node.displayName));
                EditorGUILayout.LabelField(path, m_guidStyle);
            }
        }

        private void DrawBlackboard(NodeContext context) {
            if (context?.blackboard == null)
                return;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                    EditorGUILayout.LabelField("Blackboard", m_headerStyle, GUILayout.Width(86));
                    EditorGUILayout.LabelField($"{context.blackboard.values.Count} keys", m_subtleLabelStyle, GUILayout.Width(58));
                    GUILayout.FlexibleSpace();
                }

                if (context.blackboard.values.Count == 0) {
                    EditorGUILayout.LabelField("No values", m_subtleLabelStyle);
                    return;
                }

                foreach (KeyValuePair<string, object> pair in context.blackboard.values.OrderBy(pair => pair.Key)) {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                        EditorGUILayout.LabelField(pair.Key, m_metricValueStyle, GUILayout.Width(150));
                        EditorGUILayout.LabelField(pair.Value != null ? pair.Value.GetType().Name : "null", m_guidStyle, GUILayout.Width(96));
                        EditorGUILayout.LabelField(FormatBlackboardValue(pair.Value), m_subtleLabelStyle);
                    }
                }
            }
        }

        private void DrawBlackboardChanges(NodeContext context) {
            if (context?.blackboard == null || context.blackboard.changes.Count == 0)
                return;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                    EditorGUILayout.LabelField("Blackboard Changes", m_headerStyle, GUILayout.Width(132));
                    EditorGUILayout.LabelField($"last {Math.Min(8, context.blackboard.changes.Count)}", m_subtleLabelStyle, GUILayout.Width(54));
                    GUILayout.FlexibleSpace();
                }

                foreach (BlackboardChange change in context.blackboard.changes.Skip(Math.Max(0, context.blackboard.changes.Count - 8)).Reverse()) {
                    string typeName = change.type != null ? change.type.Name : "Unknown";
                    string arrow = change.removed ? "removed" : "=";
                    string value = change.removed ? FormatBlackboardValue(change.oldValue) : FormatBlackboardValue(change.newValue);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18))) {
                        EditorGUILayout.LabelField(change.frame.ToString(), m_guidStyle, GUILayout.Width(48));
                        EditorGUILayout.LabelField(change.key, m_metricValueStyle, GUILayout.Width(150));
                        EditorGUILayout.LabelField(typeName, m_guidStyle, GUILayout.Width(96));
                        EditorGUILayout.LabelField(arrow, m_guidStyle, GUILayout.Width(56));
                        EditorGUILayout.LabelField(value, m_subtleLabelStyle);
                    }
                }
            }
        }

        private static string FormatBlackboardValue(object value) {
            return value switch {
                null => "null",
                Transform transform => transform != null ? $"{transform.name} @ {FormatVector(transform.position)}" : "Missing Transform",
                Rigidbody2D rb => rb != null ? $"{rb.name} velocity {FormatVector(rb.linearVelocity)}" : "Missing Rigidbody2D",
                Animator animator => animator != null ? animator.name : "Missing Animator",
                IEnumerable<Transform> transforms => string.Join(", ", transforms.Select(transform => transform != null ? transform.name : "Missing")),
                Vector2 vector => FormatVector(vector),
                Vector3 vector => FormatVector(vector),
                float number => number.ToString("0.###"),
                double number => number.ToString("0.###"),
                _ => value.ToString()
            };
        }

        private static string FormatVector(Vector2 vector) {
            return $"({vector.x:0.##}, {vector.y:0.##})";
        }

        private static string FormatVector(Vector3 vector) {
            return $"({vector.x:0.##}, {vector.y:0.##}, {vector.z:0.##})";
        }

        private void DrawStatusPill(string text, Color color) {
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, m_statusPillStyle, GUILayout.Width(58), GUILayout.Height(18));
            GUI.backgroundColor = oldColor;
        }

        private void DrawEmptyState(string title, string body) {
            EditorGUILayout.Space(12);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField(title, m_headerStyle);
                EditorGUILayout.LabelField(body, m_subtleLabelStyle);
            }
        }

        private bool MatchesFilter(RuntimeNode node) {
            if (string.IsNullOrWhiteSpace(m_filter))
                return true;

            string filter = m_filter.ToLowerInvariant();
            return node.displayName.ToLowerInvariant().Contains(filter) ||
                   node.GetType().Name.ToLowerInvariant().Contains(filter) ||
                   node.guid.ToLowerInvariant().Contains(filter);
        }

        private static Color GetStatusColor(NodeStatus status, bool visitedThisTick) {
            if (!visitedThisTick)
                return new Color(0.28f, 0.28f, 0.28f);

            return status switch {
                NodeStatus.Success => new Color(0.20f, 0.75f, 0.32f),
                NodeStatus.Failure => new Color(0.90f, 0.22f, 0.20f),
                NodeStatus.Running => new Color(1.00f, 0.72f, 0.18f),
                _ => Color.gray
            };
        }

        private static string StatusShortName(NodeStatus status) {
            return status switch {
                NodeStatus.Success => "OK",
                NodeStatus.Failure => "Fail",
                NodeStatus.Running => "Run",
                _ => status.ToString()
            };
        }

        private static string GetDebugReason(RuntimeNodeDebugData debug) {
            if (debug == null)
                return "--";

            if (!string.IsNullOrWhiteSpace(debug.statusReason))
                return debug.statusReason;

            return debug.tickCount > 0
                ? $"Last status changed on frame {debug.lastStatusChangeFrame}"
                : "Not ticked yet";
        }

        private static string ShortGuid(string guid) {
            if (string.IsNullOrEmpty(guid) || guid.Length <= 8)
                return guid;

            return guid[..8];
        }

        private void EnsureStyles() {
            if (m_headerStyle != null)
                return;

            m_cardTexture = CreateTexture(new Color(0.18f, 0.18f, 0.18f, 1f));
            m_rowTexture = CreateTexture(new Color(0.15f, 0.15f, 0.15f, 1f));
            m_rowAltTexture = CreateTexture(new Color(0.13f, 0.13f, 0.13f, 1f));

            m_headerStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_headerStyle, new Color(0.92f, 0.92f, 0.92f));

            m_subtleLabelStyle = new GUIStyle(EditorStyles.label) {
                wordWrap = false
            };
            SetTextColor(m_subtleLabelStyle, new Color(0.72f, 0.72f, 0.72f));

            m_metricValueStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 11,
                clipping = TextClipping.Ellipsis,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_metricValueStyle, new Color(0.90f, 0.90f, 0.90f));

            m_metricLabelStyle = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Ellipsis
            };
            SetTextColor(m_metricLabelStyle, new Color(0.58f, 0.58f, 0.58f));

            m_cardStyle = new GUIStyle(EditorStyles.helpBox) {
                normal = { background = m_cardTexture },
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(4, 4, 2, 2)
            };

            m_nodeRowStyle = new GUIStyle(EditorStyles.helpBox) {
                normal = { background = m_rowTexture },
                padding = new RectOffset(6, 6, 3, 3),
                margin = new RectOffset(4, 4, 1, 1)
            };

            m_nodeRowAltStyle = new GUIStyle(m_nodeRowStyle) {
                normal = { background = m_rowAltTexture }
            };

            m_columnHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel) {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis
            };
            SetTextColor(m_columnHeaderStyle, new Color(0.60f, 0.60f, 0.60f));

            m_statusPillStyle = new GUIStyle(EditorStyles.miniButton) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            SetTextColor(m_statusPillStyle, Color.white);

            m_guidStyle = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Ellipsis
            };
            SetTextColor(m_guidStyle, new Color(0.52f, 0.52f, 0.52f));

            m_monoStyle = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis
            };
            SetTextColor(m_monoStyle, new Color(0.72f, 0.72f, 0.72f));

            m_toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            SetTextColor(m_toolbarButtonStyle, new Color(0.90f, 0.90f, 0.90f));

            m_toolbarSearchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField);
            SetTextColor(m_toolbarSearchFieldStyle, Color.white);

            m_miniButtonStyle = new GUIStyle(EditorStyles.miniButton);
            SetTextColor(m_miniButtonStyle, new Color(0.90f, 0.90f, 0.90f));
        }

        private static void SetTextColor(GUIStyle style, Color color) {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static Texture2D CreateTexture(Color color) {
            Texture2D texture = new(1, 1) {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyTexture(Texture2D texture) {
            if (texture != null) {
                DestroyImmediate(texture);
            }
        }

        private void DrawWindowBackground() {
            if (Event.current.type == EventType.Repaint) {
                EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), new Color(0.12f, 0.12f, 0.12f));
            }
        }
    }
}
