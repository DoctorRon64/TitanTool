using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    public sealed class TitanToolDocumentationWindow : EditorWindow {
        private Vector2 m_scroll;
        private string m_filter = string.Empty;
        private BossGraphNodeCategory? m_categoryFilter;
        private GUIStyle m_titleStyle;
        private GUIStyle m_headerStyle;
        private GUIStyle m_subtleStyle;
        private GUIStyle m_cardStyle;
        private GUIStyle m_nodeTitleStyle;
        private GUIStyle m_badgeStyle;
        private GUIStyle m_wrapStyle;
        private Texture2D m_cardTexture;

        private static readonly DocLine[] QuickStartLines = {
            new("1", "Create Graph", "Add a BossDirector to the boss object, then use Make New Graph or assign an existing .titan graph."),
            new("2", "Scene References", "Assign the player, animator, sprite renderer, and TargetPointProvider before testing the graph."),
            new("3", "Build Flow", "Connect Start to one main branch. Use composites to choose order, decorators to gate behavior, and actions to do gameplay work."),
            new("4", "Play And Debug", "Enter Play Mode. Active nodes and followed wires highlight in the graph; the Runtime Debugger shows live values.")
        };

        private static readonly DocLine[] ExecutionLines = {
            new("Start", "Entry Point", "Every graph has exactly one Start node. It is created automatically and cannot be added or deleted manually."),
            new("Tick", "BossDirector", "The BossDirector ticks the compiled graph. Nodes that return Running are visited again on later ticks."),
            new("Wire", "Connected Only", "Only nodes reachable from Start are part of execution. Disconnected nodes are ignored and shown as inactive graph clutter."),
            new("Parent", "Status Rules", "Composite and decorator nodes decide what to do with child Success, Failure, or Running results."),
            new("Action", "Gameplay Result", "Actions move, shoot, spawn, throw, animate, wait, or write runtime data.")
        };

        private static readonly DocLine[] StatusLines = {
            new("Running", "Still Active", "The node is not done yet. Parent nodes usually keep the branch active and tick it again later."),
            new("Success", "Completed", "The node did what it was supposed to do. Sequence branches usually continue to the next child."),
            new("Failure", "Blocked Or False", "The node could not run or a condition was false. Sequences usually stop; selectors may try another child.")
        };

        private static readonly DocLine[] NodeChoiceLines = {
            new("Order", "Run In Order", "Use Sequence when several steps must all finish in order, such as move, wait, then shoot."),
            new("Choice", "Try Children", "Use Selector when the first valid branch should win, such as phase behavior with a fallback."),
            new("Together", "Run Together", "Use Parallel when unfinished branches must tick together, such as moving while shooting. Completed branches wait until the preset finishes the group."),
            new("Repeat", "Repeat Sequence", "Use Repeat Sequence when one small child group should run several times without copying the same nodes. Set After Completion to Remember Success when it should not replay after finishing once."),
            new("Custom", "Custom Scriptable Node", "Use Custom Scriptable Node for project-specific boss actions, polish package calls, and one-off behavior without changing the TitanTool package."),
            new("Random", "Random Nodes", "Use Pick Random Child for one random branch, Shuffle Bag for no-repeat variety, and RandomVariable for random values.")
        };

        private static readonly DocLine[] ParallelLines = {
            new("FailFast", "Fail Fast", "All children must succeed, but the parallel node fails immediately when any child fails."),
            new("AnyOK", "Any Success Wins", "The first successful child succeeds the parallel node. It fails only when every child failed."),
            new("Wait", "Wait For All", "Every child gets a chance to finish. The node succeeds only when every child succeeded."),
            new("First", "First Result Wins", "Any success or failure can finish the node. If success and failure happen in the same update, failure wins.")
        };

        private static readonly DocLine[] PatternLines = {
            new("Move", "Move While Shooting", "Use Run Together with one movement branch and one shooting branch. Completed shot branches wait while movement keeps running."),
            new("Repeat", "Repeat Attack Pattern", "Use Repeat Sequence around a small shoot, wait, move, or spawn group instead of copying those nodes."),
            new("Gate", "Blackboard Counter Gate", "Use RuntimeMath and RuntimeCompare to count attempts, then reset the value after the gate passes."),
            new("Intro", "Run Once Intro", "Use Run Once for a one-time opening animation or attack before normal phase logic starts.")
        };

        private static readonly DocLine[] TargetLines = {
            new("Provider", "Scene Locations", "Use a TargetPointProvider scene object to collect or create TargetPoint objects."),
            new("Keys", "Stable Link", "Graphs still store TargetPointKey assets because .titan assets should not depend directly on scene object references."),
            new("Rename", "Keep Clean", "Use Rename Keys From Objects to sync key asset names with the matching scene object names."),
            new("Warnings", "Missing Scene Key", "The graph warns when a node uses a TargetPointKey that is not present in the current TargetPointProvider.")
        };

        private static readonly DocLine[] BlackboardLines = {
            new("Memory", "Shared Runtime Data", "The blackboard stores boss references, health, target data, counters, and temporary decisions."),
            new("Write", "RuntimeMathNode", "Writes or modifies numeric runtime variables."),
            new("Read", "RuntimeCompareNode", "Checks numeric runtime variables to decide whether a branch can continue."),
            new("Random", "RandomVariableNode", "Feeds random int, float, or Vector2 values into compatible ports."),
            new("Target", "RandomTargetPointKeyNode", "Picks one assigned TargetPointKey at random for target point inputs."),
            new("Source", "Value Source Chips", "Graph nodes show compact CONST, WIRE, VAR, and BB chips so you can see whether inputs come from inline constants, connected nodes, graph variables, or blackboard keys.")
        };

        private static readonly DocLine[] SearchLines = {
            new("Name", "Search By Node", "Search accepts visible names, runtime class names, categories, tooltips, and keywords."),
            new("Alias", "Search By Intent", "Common words are supported where useful: bullet and projectile find Shoot, bomb finds Throw."),
            new("Docs", "Same Source", "Hover text and this node reference use the same node registry, so documentation stays aligned with the add-node menu.")
        };

        [MenuItem("Window/TitanTool/Documentation")]
        private static void OpenMenu() {
            OpenWindow();
        }

        public static TitanToolDocumentationWindow OpenWindow() {
            TitanToolDocumentationWindow window = GetWindow<TitanToolDocumentationWindow>("TitanTool Docs");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
            return window;
        }

        private void OnDisable() {
            if (m_cardTexture != null)
                DestroyImmediate(m_cardTexture);

            m_cardTexture = null;
            m_titleStyle = null;
        }

        private void OnGUI() {
            EnsureStyles();
            DrawToolbar();

            IReadOnlyList<GraphNodeRegistration> registrations = GetVisibleRegistrations();

            m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
            DrawHero();
            DrawDocGrid();
            DrawNodeSummary(registrations.Count, NodeTypeRegistry.GetRegistrations().Count);
            DrawNodeReference(registrations);
            DrawValueNodeReference();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                EditorGUILayout.LabelField("Search Nodes", m_subtleStyle, GUILayout.Width(78));
                m_filter = GUILayout.TextField(m_filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(180));

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(m_filter))) {
                    if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22))) {
                        m_filter = string.Empty;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.Space(6);
                DrawCategoryFilter();
            }
        }

        private void DrawCategoryFilter() {
            List<string> labels = new() { "All" };
            labels.AddRange(Enum.GetNames(typeof(BossGraphNodeCategory)));
            string[] options = labels.ToArray();
            int selectedIndex = m_categoryFilter.HasValue ? Array.IndexOf(options, m_categoryFilter.Value.ToString()) : 0;
            int newIndex = EditorGUILayout.Popup(selectedIndex, options, EditorStyles.toolbarPopup, GUILayout.Width(120));
            m_categoryFilter = newIndex <= 0 ? null : (BossGraphNodeCategory)Enum.Parse(typeof(BossGraphNodeCategory), labels[newIndex]);
        }

        private IReadOnlyList<GraphNodeRegistration> GetVisibleRegistrations() {
            string filter = m_filter?.Trim();

            return NodeTypeRegistry.GetRegistrations()
                .Where(registration => !m_categoryFilter.HasValue || registration.category == m_categoryFilter.Value)
                .Where(registration => MatchesFilter(registration, filter))
                .OrderBy(registration => registration.category)
                .ThenBy(registration => registration.menuPath)
                .ThenBy(GetDocumentationNodeName)
                .ToList();
        }

        private static bool MatchesFilter(GraphNodeRegistration registration, string filter) {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return Contains(GetDocumentationNodeName(registration), filter) ||
                   Contains(registration.displayName, filter) ||
                   Contains(registration.tooltip, filter) ||
                   Contains(registration.searchKeywords, filter) ||
                   Contains(registration.menuPath, filter) ||
                   Contains(registration.runtimeType.Name, filter) ||
                   Contains(registration.editorType.Name, filter);
        }

        private static bool Contains(string value, string filter) {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawHero() {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField("TitanTool Documentation", m_titleStyle);
                EditorGUILayout.LabelField("Compact reference for boss graph setup, execution flow, statuses, scene target points, value nodes, and every available node.", m_wrapStyle);
            }
        }

        private void DrawDocGrid() {
            DrawTextSection("Quick Start", QuickStartLines);
            DrawTextSection("Execution Flow", ExecutionLines);
            DrawTextSection("Success / Failure / Running", StatusLines);
            DrawTextSection("Choosing Nodes", NodeChoiceLines);
            DrawTextSection("Parallel Presets", ParallelLines);
            DrawTextSection("Example Patterns", PatternLines);
            DrawTextSection("Target Points", TargetLines);
            DrawTextSection("Blackboard And Values", BlackboardLines);
            DrawTextSection("Search And Hover Text", SearchLines);
        }

        private void DrawTextSection(string title, IReadOnlyList<DocLine> lines) {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField(title, m_headerStyle);

                foreach (DocLine line in lines)
                    DrawDocRow(line);
            }
        }

        private void DrawDocRow(DocLine line) {
            using (new EditorGUILayout.HorizontalScope()) {
                DrawSmallBadge(line.badge, 62f);
                using (new EditorGUILayout.VerticalScope()) {
                    EditorGUILayout.LabelField(line.title, m_nodeTitleStyle);
                    EditorGUILayout.LabelField(line.text, m_wrapStyle);
                }
            }
        }

        private void DrawNodeSummary(int visibleCount, int totalCount) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Node Reference", m_headerStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{visibleCount} of {totalCount}", m_subtleStyle, GUILayout.Width(76));
            }
        }

        private void DrawNodeReference(IReadOnlyList<GraphNodeRegistration> registrations) {
            if (registrations.Count == 0) {
                DrawEmptyState();
                return;
            }

            foreach (IGrouping<BossGraphNodeCategory, GraphNodeRegistration> group in registrations.GroupBy(registration => registration.category)) {
                using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField(group.Key.ToString(), m_headerStyle);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(group.Count().ToString(), m_subtleStyle, GUILayout.Width(28));
                    }

                    foreach (GraphNodeRegistration registration in group)
                        DrawNodeRow(registration);
                }
            }
        }

        private void DrawNodeRow(GraphNodeRegistration registration) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(GetDocumentationNodeName(registration), m_nodeTitleStyle, GUILayout.Width(168));
                DrawSmallBadge(registration.category.ToString(), 76f);
                EditorGUILayout.LabelField(registration.tooltip, m_wrapStyle);
            }
        }

        private void DrawValueNodeReference() {
            if (m_categoryFilter.HasValue && m_categoryFilter.Value != BossGraphNodeCategory.Utility)
                return;

            if (!string.IsNullOrWhiteSpace(m_filter) &&
                !Contains("RandomVariableNode", m_filter) &&
                !Contains("Random Variable", m_filter) &&
                !Contains("RandomTargetPointKeyNode", m_filter) &&
                !Contains("Random Target Point Key", m_filter) &&
                !Contains("random variable value int float vector2 target point key position", m_filter))
                return;

            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField("Value Nodes", m_headerStyle);
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("RandomVariableNode", m_nodeTitleStyle, GUILayout.Width(168));
                    DrawSmallBadge(BossGraphNodeCategory.Utility.ToString(), 76f);
                    EditorGUILayout.LabelField("Outputs a random float, int, or Vector2 between Min and Max. Connect it to compatible value ports.", m_wrapStyle);
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("RandomTargetPointKeyNode", m_nodeTitleStyle, GUILayout.Width(168));
                    DrawSmallBadge(BossGraphNodeCategory.Utility.ToString(), 76f);
                    EditorGUILayout.LabelField("Outputs one assigned TargetPointKey at random. Target Points controls the number of key slots, with a minimum of 2; missing scene keys are warned.", m_wrapStyle);
                }
            }
        }

        private void DrawSmallBadge(string text, float width) {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.40f, 0.62f);
            GUILayout.Label(text, m_badgeStyle, GUILayout.Width(width), GUILayout.Height(18));
            GUI.backgroundColor = previousColor;
        }

        private void DrawEmptyState() {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField("No matching nodes", m_headerStyle);
                EditorGUILayout.LabelField("Clear the search or choose a different category.", m_subtleStyle);
            }
        }

        private static string GetDocumentationNodeName(GraphNodeRegistration registration) {
            return registration.runtimeType?.Name ?? registration.editorType.Name;
        }

        private void EnsureStyles() {
            if (m_titleStyle != null)
                return;

            m_cardTexture = CreateTexture(new Color(0.18f, 0.18f, 0.18f, 1f));

            m_titleStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_titleStyle, new Color(0.96f, 0.96f, 0.96f));

            m_headerStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_headerStyle, new Color(0.92f, 0.92f, 0.92f));

            m_subtleStyle = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Ellipsis
            };
            SetTextColor(m_subtleStyle, new Color(0.70f, 0.70f, 0.70f));

            m_nodeTitleStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_nodeTitleStyle, new Color(0.90f, 0.90f, 0.90f));

            m_wrapStyle = new GUIStyle(EditorStyles.label) {
                wordWrap = true,
                fontSize = 11
            };
            SetTextColor(m_wrapStyle, new Color(0.78f, 0.78f, 0.78f));

            m_badgeStyle = new GUIStyle(EditorStyles.miniButton) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 9
            };
            SetTextColor(m_badgeStyle, Color.white);

            m_cardStyle = new GUIStyle(EditorStyles.helpBox) {
                normal = { background = m_cardTexture },
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(4, 4, 3, 4)
            };
        }

        private static Texture2D CreateTexture(Color color) {
            Texture2D texture = new(1, 1) {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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

        private readonly struct DocLine {
            public DocLine(string badge, string title, string text) {
                this.badge = badge;
                this.title = title;
                this.text = text;
            }

            public readonly string badge;
            public readonly string title;
            public readonly string text;
        }
    }
}
