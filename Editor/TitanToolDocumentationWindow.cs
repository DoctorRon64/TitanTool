using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace TitanTool.Editor {
    public sealed class TitanToolDocumentationWindow : EditorWindow {
        private const string DocumentationFileName = "TitanTool Documentation.docx";

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
            DrawNodeSummary(registrations.Count, NodeTypeRegistry.GetRegistrations().Count);

            foreach (IGrouping<BossGraphNodeCategory, GraphNodeRegistration> group in registrations.GroupBy(registration => registration.category)) {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(group.Key.ToString(), m_headerStyle);

                foreach (GraphNodeRegistration registration in group)
                    DrawNodeCard(registration);
            }

            if (registrations.Count == 0)
                DrawEmptyState();

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
                EditorGUILayout.LabelField("Open the package documentation or browse the node reference below.", m_subtleStyle);

                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Open Documentation", GUILayout.Width(150)))
                        OpenDocumentationFile();

                    if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                        OpenDocumentationFolder();

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawNodeSummary(int visibleCount, int totalCount) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Node Reference", m_headerStyle);
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
                    EditorGUILayout.LabelField("Menu", m_subtleStyle, GUILayout.Width(58));
                    EditorGUILayout.LabelField(CombineMenuPath(registration.menuPath, registration.displayName), m_subtleStyle);
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Runtime", m_subtleStyle, GUILayout.Width(58));
                    EditorGUILayout.LabelField(registration.runtimeType.Name, m_subtleStyle);
                }
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

        private static string CombineMenuPath(string menuPath, string displayName) {
            if (string.IsNullOrWhiteSpace(menuPath))
                return displayName;

            return menuPath.TrimEnd('/') + "/" + displayName;
        }

        private static void OpenDocumentationFile() {
            string path = GetDocumentationFilePath();
            if (File.Exists(path)) {
                EditorUtility.OpenWithDefaultApp(path);
                return;
            }

            Debug.LogWarning($"TitanTool documentation file not found: {path}");
        }

        private static void OpenDocumentationFolder() {
            string path = GetDocumentationFolderPath();
            if (Directory.Exists(path)) {
                EditorUtility.RevealInFinder(path);
                return;
            }

            Debug.LogWarning($"TitanTool documentation folder not found: {path}");
        }

        private static string GetDocumentationFilePath() {
            return Path.Combine(GetDocumentationFolderPath(), DocumentationFileName);
        }

        private static string GetDocumentationFolderPath() {
            PackageManagerInfo package = PackageManagerInfo.FindForAssembly(typeof(TitanToolDocumentationWindow).Assembly);
            string packagePath = package != null ? package.resolvedPath : Application.dataPath;
            return Path.Combine(packagePath, "Documentation~");
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
