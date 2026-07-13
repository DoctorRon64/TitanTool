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
        private GUIStyle m_headerStyle;
        private GUIStyle m_subtleStyle;
        private GUIStyle m_cardStyle;
        private GUIStyle m_nodeTitleStyle;
        private GUIStyle m_badgeStyle;
        private GUIStyle m_wrapStyle;
        private Texture2D m_cardTexture;

        private static readonly string[] OverviewLines = {
            "TitanTool is a Unity editor tool for designing boss behaviour as a visual graph.",
            "Graphs are validated in the editor, compiled into BossGraphAsset runtime data, and executed in Play Mode by a BossDirector.",
            "Use it to author boss phases, movement, timing, shooting, spawning, throwing, animation, conditions, and shared blackboard values without hard-coding every sequence."
        };

        private static readonly string[] SetupLines = {
            "Add a BossDirector to the boss object and assign the graph plus the required scene references such as animator, sprite renderer, player transform, rigidbody, health, pools, and target points.",
            "Use Scene Points collects TargetPoint objects from the scene. Create Missing Target Point Keys creates reusable TargetPointKey assets under Data/TitanTool/TargetPoints.",
            "Make New Graph creates a .titan graph under Data/TitanTool. Open Graph opens the visual editor for that graph."
        };

        private static readonly string[] ExecutionLines = {
            "Every graph owns exactly one Start node. It is created automatically, cannot be added manually, and cannot be deleted.",
            "Execution flows through wires from parent nodes to child nodes. Each tick, a node returns Success, Failure, or Running.",
            "Composite nodes shape the branch: Run In Order stops at the first running or failed child, Try Children stops when a child succeeds, random selectors choose a branch, and Run In Parallel ticks multiple branches.",
            "Decorator and condition nodes decide whether a child is allowed to run. Action nodes create the visible gameplay result.",
            "During Play Mode the graph highlights live execution: active nodes show status badges, followed execution wires become brighter, and the Runtime Debugger shows visited nodes, timings, and blackboard values."
        };

        private static readonly string[] BlackboardLines = {
            "The blackboard is shared runtime memory for graph values such as boss references, health values, target data, counters, and temporary decisions.",
            "Change Blackboard Number writes or modifies numeric values. Check Blackboard Number reads those values to decide whether a branch can continue.",
            "Random Constant can feed random int, float, or Vector2 values into compatible node ports."
        };

        [MenuItem("Window/TitanTool/Documentation")]
        private static void Open() {
            GetWindow<TitanToolDocumentationWindow>("TitanTool Docs");
        }

        private void OnDisable() {
            if (m_cardTexture != null)
                DestroyImmediate(m_cardTexture);

            m_cardTexture = null;
            m_headerStyle = null;
        }

        private void OnGUI() {
            EnsureStyles();
            DrawToolbar();

            IReadOnlyList<GraphNodeRegistration> registrations = GetVisibleRegistrations();

            m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
            DrawDocumentationCard();
            DrawBuiltInDocumentation();
            DrawNodeSummary(registrations.Count, NodeTypeRegistry.GetRegistrations().Count);

            foreach (IGrouping<BossGraphNodeCategory, GraphNodeRegistration> group in registrations.GroupBy(registration => registration.category)) {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(group.Key.ToString(), m_headerStyle);

                foreach (GraphNodeRegistration registration in group)
                    DrawNodeCard(registration);
            }

            if (registrations.Count == 0)
                DrawEmptyState();

            DrawValueNodeReference();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
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
            List<string> labels = new List<string> { "All" };
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
                .ThenBy(registration => registration.displayName)
                .ToList();
        }

        private static bool MatchesFilter(GraphNodeRegistration registration, string filter) {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return Contains(registration.displayName, filter) ||
                   Contains(registration.tooltip, filter) ||
                   Contains(registration.menuPath, filter) ||
                   Contains(registration.runtimeType.Name, filter) ||
                   Contains(registration.editorType.Name, filter);
        }

        private static bool Contains(string value, string filter) {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawDocumentationCard() {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField("TitanTool Documentation", m_headerStyle);
                EditorGUILayout.LabelField("Package overview, setup notes, execution flow, and current node reference.", m_subtleStyle);
            }
        }

        private void DrawBuiltInDocumentation() {
            DrawTextSection("Overview", OverviewLines);
            DrawTextSection("Setup", SetupLines);
            DrawTextSection("Execution Flow", ExecutionLines);
            DrawTextSection("Blackboard And Values", BlackboardLines);
        }

        private void DrawTextSection(string title, IReadOnlyList<string> lines) {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField(title, m_headerStyle);

                foreach (string line in lines)
                    DrawBullet(line);
            }
        }

        private void DrawBullet(string text) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("-", m_subtleStyle, GUILayout.Width(14));
                EditorGUILayout.LabelField(text, m_wrapStyle);
            }
        }

        private void DrawNodeSummary(int visibleCount, int totalCount) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Addable Node Reference", m_headerStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{visibleCount} of {totalCount} nodes", m_subtleStyle, GUILayout.Width(110));
            }
        }

        private void DrawNodeCard(GraphNodeRegistration registration) {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField(registration.displayName, m_nodeTitleStyle);
                    GUILayout.FlexibleSpace();
                    DrawBadge(registration.category.ToString());
                }

                EditorGUILayout.LabelField(registration.tooltip, m_wrapStyle);

                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Add Menu", m_subtleStyle, GUILayout.Width(68));
                    EditorGUILayout.LabelField(FormatMenuPath(registration.menuPath, registration.displayName), m_subtleStyle);
                }
            }
        }

        private void DrawValueNodeReference() {
            if (m_categoryFilter.HasValue && m_categoryFilter.Value != BossGraphNodeCategory.Utility)
                return;

            if (!string.IsNullOrWhiteSpace(m_filter) &&
                !Contains("Random Constant", m_filter) &&
                !Contains("random value int float vector2", m_filter))
                return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Value Nodes", m_headerStyle);

            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Random Constant", m_nodeTitleStyle);
                    GUILayout.FlexibleSpace();
                    DrawBadge(BossGraphNodeCategory.Utility.ToString());
                }

                EditorGUILayout.LabelField("Outputs a random float, int, or Vector2 between a minimum and maximum range for compatible node ports.", m_wrapStyle);
                EditorGUILayout.LabelField("Add Menu: create it from the GraphToolkit node search as Random Constant.", m_subtleStyle);
            }
        }

        private void DrawBadge(string text) {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.40f, 0.62f);
            GUILayout.Label(text, m_badgeStyle, GUILayout.Width(84), GUILayout.Height(18));
            GUI.backgroundColor = previousColor;
        }

        private void DrawEmptyState() {
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField("No matching nodes", m_headerStyle);
                EditorGUILayout.LabelField("Clear the search or choose a different category.", m_subtleStyle);
            }
        }

        private static string FormatMenuPath(string menuPath, string displayName) {
            if (string.IsNullOrWhiteSpace(menuPath))
                return displayName;

            return menuPath.TrimEnd('/').Replace("/", " / ") + " / " + displayName;
        }

        private void EnsureStyles() {
            if (m_headerStyle != null)
                return;

            m_cardTexture = CreateTexture(new Color(0.18f, 0.18f, 0.18f, 1f));

            m_headerStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_headerStyle, new Color(0.92f, 0.92f, 0.92f));

            m_subtleStyle = new GUIStyle(EditorStyles.label) {
                clipping = TextClipping.Ellipsis
            };
            SetTextColor(m_subtleStyle, new Color(0.70f, 0.70f, 0.70f));

            m_nodeTitleStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(m_nodeTitleStyle, new Color(0.92f, 0.92f, 0.92f));

            m_wrapStyle = new GUIStyle(EditorStyles.label) {
                wordWrap = true
            };
            SetTextColor(m_wrapStyle, new Color(0.78f, 0.78f, 0.78f));

            m_badgeStyle = new GUIStyle(EditorStyles.miniButton) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            SetTextColor(m_badgeStyle, Color.white);

            m_cardStyle = new GUIStyle(EditorStyles.helpBox) {
                normal = { background = m_cardTexture },
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(4, 4, 3, 3)
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
    }
}
