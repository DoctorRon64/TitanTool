using System;
using System.Collections.Generic;
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
            DrawSpawnPointsCard();

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
                DrawProperty("<team>k__BackingField", "Team");

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

        private void DrawSpawnPointsCard() {
            SerializedProperty spawnPoints = serializedObject.FindProperty("m_spawnPoints");
            if (spawnPoints == null)
                return;

            DrawCard("Target Points", () => {
                int childPointCount = ((BossDirector)target).GetComponentsInChildren<TargetPoint>(true).Count(point => point != null);
                int scenePointCount = FindObjectsByType<TargetPoint>(FindObjectsSortMode.None).Count(point => point != null);

                using (new EditorGUILayout.HorizontalScope()) {
                    DrawMetric("Manual", spawnPoints.arraySize.ToString());
                    DrawMetric("Children", childPointCount.ToString());
                    DrawMetric("Scene", scenePointCount.ToString());
                }

                EditorGUILayout.PropertyField(spawnPoints, new GUIContent("Override Target Points"), true);

                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Setup Child Points", GUILayout.Height(24))) {
                        CollectTargetPoints(spawnPoints, ((BossDirector)target).GetComponentsInChildren<TargetPoint>(true), true);
                    }

                    if (GUILayout.Button("Use Scene Points", GUILayout.Height(24))) {
                        CollectTargetPoints(spawnPoints, FindObjectsByType<TargetPoint>(FindObjectsSortMode.None), true);
                    }
                }

                if (spawnPoints.arraySize <= 0) {
                    DrawStatusLine("Automatic lookup", "Empty override list: runtime will use all TargetPoint components in the scene.", MessageType.Info);
                    return;
                }

                int missingKeyCount = CountMissingKeys(spawnPoints);
                using (new EditorGUI.DisabledScope(missingKeyCount == 0)) {
                    if (GUILayout.Button(missingKeyCount > 0 ? $"Create Missing Target Point Keys ({missingKeyCount})" : "Create Missing Target Point Keys", GUILayout.Height(24)))
                        EnsureTargetPointKeys(GetSpawnPoints(spawnPoints));
                }

                DrawSpawnPointSummary(spawnPoints);
            });
        }

        private void DrawSpawnPointSummary(SerializedProperty spawnPoints) {
            HashSet<TargetPointKey> seenKeys = new();
            bool foundDuplicate = false;

            using (new EditorGUI.DisabledScope(true)) {
                for (int i = 0; i < spawnPoints.arraySize; i++) {
                    TargetPoint point = spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue as TargetPoint;
                    string keyName = point != null && point.key != null ? point.key.name : "No Key";
                    string label = point != null ? $"{keyName} ({point.name})" : "Missing";
                    EditorGUILayout.TextField($"Index {i}", label);

                    if (point?.key != null && !seenKeys.Add(point.key))
                        foundDuplicate = true;
                }
            }

            if (foundDuplicate)
                DrawStatusLine("Duplicate keys", "TargetPoint lookup will use the first point with each duplicated key.", MessageType.Warning);
        }

        private void DrawCard(string title, Action content) {
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(m_cardStyle)) {
                EditorGUILayout.LabelField(title, m_headerStyle);
                GUILayout.Space(3);
                content?.Invoke();
            }
        }

        private void DrawMetric(string label, string value) {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(82))) {
                EditorGUILayout.LabelField(value, m_statusStyle);
                EditorGUILayout.LabelField(label, m_subtleStyle);
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
                GUILayout.Label("●", m_statusStyle, GUILayout.Width(16));
                GUI.color = oldColor;
                EditorGUILayout.LabelField(title, m_statusStyle, GUILayout.Width(112));
                EditorGUILayout.LabelField(message, m_subtleStyle);
            }

            GUI.color = oldColor;
        }

        private void CollectTargetPoints(SerializedProperty spawnPoints, IEnumerable<TargetPoint> targetPoints, bool createMissingKeys) {
            Undo.RecordObject(target, "Collect Spawn Points");

            TargetPoint[] orderedTargetPoints = targetPoints
                .Where(point => point != null)
                .OrderBy(point => point.key != null ? point.key.name : point.name)
                .ThenBy(point => point.name)
                .ToArray();

            if (createMissingKeys)
                EnsureTargetPointKeys(orderedTargetPoints);

            spawnPoints.arraySize = orderedTargetPoints.Length;
            for (int i = 0; i < orderedTargetPoints.Length; i++)
                spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = orderedTargetPoints[i];

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static IEnumerable<TargetPoint> GetSpawnPoints(SerializedProperty spawnPoints) {
            for (int i = 0; i < spawnPoints.arraySize; i++) {
                if (spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue is TargetPoint point)
                    yield return point;
            }
        }

        private static int CountMissingKeys(SerializedProperty spawnPoints) {
            int count = 0;
            for (int i = 0; i < spawnPoints.arraySize; i++) {
                if (spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue is TargetPoint point && point.key == null)
                    count++;
            }

            return count;
        }

        private static void EnsureTargetPointKeys(IEnumerable<TargetPoint> targetPoints) {
            BossGraph.EnsureAssetFolder();
            string folderPath = $"{AssetPath.ASSET_PATH}/TargetPoints";
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(AssetPath.ASSET_PATH, "TargetPoints");

            foreach (TargetPoint point in targetPoints.Where(point => point != null && point.key == null)) {
                TargetPointKey key = ScriptableObject.CreateInstance<TargetPointKey>();
                key.name = MakeSafeFileName(point.name);

                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{key.name}.asset");
                AssetDatabase.CreateAsset(key, assetPath);

                SerializedObject pointObject = new(point);
                pointObject.FindProperty("m_key").objectReferenceValue = key;
                pointObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(point);
            }

            AssetDatabase.SaveAssets();
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
