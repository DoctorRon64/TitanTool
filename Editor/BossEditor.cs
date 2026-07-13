using System;
using System.IO;
using System.Linq;
using TitanTool.Runtime;
using TitanTool.Runtime.Data;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    [CustomEditor(typeof(BossDirector))]
    public class BossEditor : UnityEditor.Editor {
        private GUIStyle m_cardStyle;
        private GUIStyle m_headerStyle;
        private GUIStyle m_subtleStyle;
        private GUIStyle m_statusStyle;

        public override void OnInspectorGUI() {
            EnsureStyles();

            serializedObject.Update();
            BossDirector bossDirector = (BossDirector)target;

            EditorGUILayout.Space(2);
            DrawGraphCard(bossDirector);
            DrawRuntimeCard(bossDirector);
            DrawReferencesCard();
            DrawTargetProviderCard(bossDirector);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGraphCard(BossDirector bossDirector) {
            DrawCard("Boss Graph", () => {
                SerializedProperty graph = serializedObject.FindProperty("m_graph");
                EditorGUILayout.PropertyField(graph, new GUIContent("Graph Asset"));

                BossGraphAsset graphAsset = graph.objectReferenceValue as BossGraphAsset;
                if (graphAsset == null) {
                    DrawStatusLine("No graph assigned", "Create a graph or assign an existing compiled boss graph asset.", MessageType.Warning);
                }
                else {
                    string assetPath = AssetDatabase.GetAssetPath(graphAsset);
                    bool hasRuntimeData = graphAsset.root != null || graphAsset.nodes.Count > 0;
                    DrawStatusLine(hasRuntimeData ? "Ready" : "Needs reimport", graphAsset.name, hasRuntimeData ? MessageType.Info : MessageType.Warning);

                    if (!string.IsNullOrEmpty(assetPath))
                        EditorGUILayout.LabelField(assetPath, m_subtleStyle);
                }

                GUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUI.DisabledScope(graphAsset == null)) {
                        if (GUILayout.Button("Open Graph", GUILayout.Height(24)))
                            OpenGraph(graphAsset);

                        if (GUILayout.Button("Detach Graph", GUILayout.Height(24))) {
                            Undo.RecordObject(bossDirector, "Detach Graph");
                            CloseGraphWindow(graphAsset);
                            bossDirector.SetGraph(null);
                            graph.objectReferenceValue = null;
                            EditorUtility.SetDirty(bossDirector);
                            AssetDatabase.SaveAssets();
                        }
                    }

                    using (new EditorGUI.DisabledScope(graphAsset != null)) {
                        if (GUILayout.Button("Make New Graph", GUILayout.Height(24)))
                            CreateGraph(bossDirector);
                    }
                }
            });
        }

        private void DrawRuntimeCard(BossDirector bossDirector) {
            DrawCard("Runtime", () => {
                DrawProperty("m_tickRate", "Tick Rate");
                DrawProperty("m_debugLogging", "Debug Logging");
                DrawProperty("paused", "Paused");

                GUILayout.Space(4);
                string runtimeText = Application.isPlaying
                    ? (bossDirector.context != null ? "Running" : "Waiting for runner")
                    : "Editor mode";
                MessageType runtimeType = Application.isPlaying && bossDirector.context == null ? MessageType.Warning : MessageType.Info;
                DrawStatusLine("Runtime State", runtimeText, runtimeType);
            });
        }

        private void DrawReferencesCard() {
            DrawCard("Blackboard References", () => {
                DrawProperty("m_player", "Player");
                DrawProperty("m_animator", "Animator");
                DrawProperty("m_spriteRenderer", "Sprite Renderer");

                SerializedProperty player = serializedObject.FindProperty("m_player");
                if (player.objectReferenceValue == null)
                    DrawStatusLine("Player", "Optional, but player-targeted nodes need this reference.", MessageType.Info);
            });
        }

        private void DrawTargetProviderCard(BossDirector bossDirector) {
            DrawCard("Scene Locations", () => {
                SerializedProperty targetPoints = serializedObject.FindProperty("m_targetPoints");
                EditorGUILayout.PropertyField(targetPoints, new GUIContent("Target Point Provider"));

                TargetPointProvider provider = targetPoints.objectReferenceValue as TargetPointProvider;
                if (provider == null) {
                    DrawStatusLine("No provider", "Create or assign a TargetPointProvider scene object.", MessageType.Warning);
                }
                else {
                    int pointCount = provider.GetPoints().Count(point => point != null);
                    DrawStatusLine("Provider ready", $"{provider.name} provides {pointCount} target points.", MessageType.Info);
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Create Provider", GUILayout.Height(24)))
                        CreateTargetPointProvider(bossDirector, targetPoints);

                    if (GUILayout.Button("Use Scene Provider", GUILayout.Height(24)))
                        AssignSceneProvider(targetPoints);

                    using (new EditorGUI.DisabledScope(provider == null)) {
                        if (GUILayout.Button("Select", GUILayout.Height(24)))
                            Selection.activeObject = provider;
                    }
                }
            });
        }

        private void CreateTargetPointProvider(BossDirector bossDirector, SerializedProperty targetPoints) {
            GameObject providerObject = new("Boss Target Points");
            Undo.RegisterCreatedObjectUndo(providerObject, "Create Target Point Provider");
            providerObject.transform.position = bossDirector.transform.position;

            TargetPointProvider provider = providerObject.AddComponent<TargetPointProvider>();
            Undo.RecordObject(bossDirector, "Assign Target Point Provider");
            targetPoints.objectReferenceValue = provider;
            EditorUtility.SetDirty(bossDirector);
            Selection.activeObject = providerObject;
        }

        private static void AssignSceneProvider(SerializedProperty targetPoints) {
            TargetPointProvider provider = UnityEngine.Object.FindFirstObjectByType<TargetPointProvider>();
            if (provider == null)
                return;

            targetPoints.objectReferenceValue = provider;
            Selection.activeObject = provider;
        }

        private void DrawCard(string title, Action content) {
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField(title, m_headerStyle);
                GUILayout.Space(3);
                content?.Invoke();
            }
        }

        private void DrawProperty(string propertyName, string label) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void DrawStatusLine(string title, string message, MessageType type) {
            Color oldColor = GUI.color;
            GUI.color = type switch {
                MessageType.Warning => new Color(1f, 0.74f, 0.32f),
                MessageType.Error => new Color(1f, 0.38f, 0.34f),
                _ => new Color(0.42f, 0.82f, 0.52f)
            };

            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Label("*", m_statusStyle, GUILayout.Width(16));
                GUI.color = oldColor;
                EditorGUILayout.LabelField(title, m_statusStyle, GUILayout.Width(112));
                EditorGUILayout.LabelField(message, m_subtleStyle);
            }

            GUI.color = oldColor;
        }

        private static void OpenGraph(BossGraphAsset runtimeAsset) {
            if (runtimeAsset == null)
                return;

            string runtimePath = AssetDatabase.GetAssetPath(runtimeAsset);
            string dir = Path.GetDirectoryName(runtimePath);
            string baseName = Path.GetFileNameWithoutExtension(runtimePath).Replace("_Runtime", "");
            if (dir == null)
                return;

            string graphPath = Path.Combine(dir, baseName + "." + BossGraph.ASSET_EXTENSION)
                .Replace('\\', '/');

            UnityEngine.Object graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
            if (graphAsset != null) {
                AssetDatabase.OpenAsset(graphAsset);
                return;
            }

            Debug.LogWarning($"Could not find .bossgraph at: {graphPath}");
        }

        private static void CloseGraphWindow(BossGraphAsset runtimeAsset) {
            if (runtimeAsset == null)
                return;

            string runtimePath = AssetDatabase.GetAssetPath(runtimeAsset);
            string dir = Path.GetDirectoryName(runtimePath);
            string baseName = Path.GetFileNameWithoutExtension(runtimePath).Replace("_Runtime", "");

            if (dir != null) {
                string graphPath = Path.Combine(dir, baseName + "." + BossGraph.ASSET_EXTENSION).Replace('\\', '/');
                UnityEngine.Object graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
                if (graphAsset == null)
                    return;
            }

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (window == null)
                    continue;

                if (window.titleContent.text.Contains(baseName))
                    window.Close();
            }
        }

        private static string GetGraphPath(BossDirector bossDirector) {
            string bossName = string.IsNullOrEmpty(bossDirector.name) ? "Boss" : bossDirector.name;
            string safeName = MakeSafeFileName(bossName);
            return $"{AssetPath.ASSET_PATH}/{safeName}.{BossGraph.ASSET_EXTENSION}";
        }

        private static string MakeSafeFileName(string fileName) {
            char[] invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct().ToArray();
            string safeName = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(safeName) ? "Boss" : safeName.Trim();
        }

        private void CreateGraph(BossDirector bossDirector) {
            BossGraph.EnsureAssetFolder();

            string editorGraphPath = AssetDatabase.GenerateUniqueAssetPath(GetGraphPath(bossDirector));
            GraphDatabase.CreateGraph<BossGraph>(editorGraphPath);

            AssetDatabase.ImportAsset(
                editorGraphPath,
                ImportAssetOptions.ForceUpdate
            );
            AssetDatabase.Refresh();

            BossGraphAsset runtimeGraphAsset = AssetDatabase.LoadMainAssetAtPath(editorGraphPath) as BossGraphAsset;
            if (runtimeGraphAsset == null) {
                Debug.LogError(
                    $"Failed to load runtime graph at:\n{editorGraphPath}"
                );
                return;
            }

            Undo.RecordObject(bossDirector, "Assign Boss Graph");
            bossDirector.SetGraph(runtimeGraphAsset);
            serializedObject.FindProperty("m_graph").objectReferenceValue = runtimeGraphAsset;
            EditorUtility.SetDirty(bossDirector);
            AssetDatabase.SaveAssets();
        }

        private void EnsureStyles() {
            m_cardStyle ??= new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(10, 10, 8, 9),
                margin = new RectOffset(0, 0, 4, 6)
            };
            m_headerStyle ??= new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 12
            };
            m_subtleStyle ??= new GUIStyle(EditorStyles.miniLabel) {
                wordWrap = true
            };
            m_statusStyle ??= new GUIStyle(EditorStyles.boldLabel);
        }
    }
}
