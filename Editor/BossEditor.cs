using UnityEditor;
using UnityEngine;
using Unity.GraphToolkit.Editor;
using TitanTool.Runtime;
using TitanTool.Runtime.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TitanTool.Editor {
    [CustomEditor(typeof(BossDirector))]
    public class BossEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "m_spawnPoints");
            DrawSpawnPoints();
            serializedObject.ApplyModifiedProperties();

            BossDirector bossDirector = (BossDirector)target;
            DrawGraphStatus(bossDirector);
            GUILayout.Space(10);
            DrawGraphControls(bossDirector);
        }

        private void DrawSpawnPoints() {
            SerializedProperty spawnPoints = serializedObject.FindProperty("m_spawnPoints");
            if (spawnPoints == null)
                return;

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Spawn Points", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spawnPoints, new GUIContent("Override Target Points"), true);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Setup Child Points")) {
                    CollectTargetPoints(spawnPoints, ((BossDirector)target).GetComponentsInChildren<TargetPoint>(true), true);
                }

                if (GUILayout.Button("Use Scene Points")) {
                    CollectTargetPoints(spawnPoints, FindObjectsByType<TargetPoint>(FindObjectsSortMode.None), true);
                }
            }

            if (spawnPoints.arraySize <= 0) {
                EditorGUILayout.HelpBox("Leave this empty to automatically use all TargetPoint components in the scene. Use Setup Child Points or Use Scene Points only when you want to create missing keys or lock the list.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Create Missing Target Point Keys")) {
                EnsureTargetPointKeys(GetSpawnPoints(spawnPoints));
            }

            HashSet<TargetPointKey> seenKeys = new();
            using (new EditorGUI.DisabledScope(true)) {
                for (int i = 0; i < spawnPoints.arraySize; i++) {
                    TargetPoint point = spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue as TargetPoint;
                    string keyName = point != null && point.key != null ? point.key.name : "No Key";
                    string label = point != null ? $"{keyName} ({point.name})" : "Missing";
                    EditorGUILayout.TextField($"Index {i}", label);
                    if (point?.key != null && !seenKeys.Add(point.key))
                        EditorGUILayout.HelpBox($"Duplicate TargetPoint key '{point.key.name}'. TargetPoint lookup will use the first matching point.", MessageType.Warning);
                }
            }
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

        private void DrawGraphStatus(BossDirector bossDirector) {
            GUILayout.Space(5);
            Color old = GUI.color;
            GUI.color = bossDirector.graph == null ? Color.red : Color.green;
            GUILayout.Label(bossDirector.graph == null ? "● No Graph Assigned" : $"● {bossDirector.graph.name}");
            GUI.color = old;
        }

        private void DrawGraphControls(BossDirector bossDirector) {
            GUILayout.BeginHorizontal();

            if (bossDirector.graph != null) {
                if (GUILayout.Button("Open Graph")) {
                    string runtimePath = AssetDatabase.GetAssetPath(bossDirector.graph);
                    string dir = Path.GetDirectoryName(runtimePath);
                    string baseName = Path.GetFileNameWithoutExtension(runtimePath).Replace("_Runtime", "");
                    if (dir != null) {
                        string graphPath = Path.Combine(dir, baseName + "." + BossGraph.ASSET_EXTENSION)
                            .Replace('\\', '/');

                        Object graphAsset = AssetDatabase.LoadAssetAtPath<Object>(graphPath);
                        if (graphAsset != null) {
                            AssetDatabase.OpenAsset(graphAsset);
                        }
                        else {
                            Debug.LogWarning($"Could not find .bossgraph at: {graphPath}");
                        }
                    }
                }

                if (GUILayout.Button("Detach Graph")) {
                    Undo.RecordObject(bossDirector, "Detach Graph");
                    CloseGraphWindow(bossDirector.graph);
                    bossDirector.SetGraph(null);
                    EditorUtility.SetDirty(bossDirector);
                    AssetDatabase.SaveAssets();
                }
            }
            else {
                if (GUILayout.Button("Make New Graph")) {
                    CreateGraph(bossDirector);
                }
            }

            GUILayout.EndHorizontal();
        }

        private void CloseGraphWindow(BossGraphAsset runtimeAsset) {
            if (runtimeAsset == null) return;

            string runtimePath = AssetDatabase.GetAssetPath(runtimeAsset);
            string dir = Path.GetDirectoryName(runtimePath);
            string baseName = Path.GetFileNameWithoutExtension(runtimePath).Replace("_Runtime", "");

            if (dir != null) {
                string graphPath = Path.Combine(dir, baseName + "." + BossGraph.ASSET_EXTENSION).Replace('\\', '/');
                Object graphAsset = AssetDatabase.LoadAssetAtPath<Object>(graphPath);
                if (graphAsset == null) return;
            }

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (window == null) continue;
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

            // Create graph
            GraphDatabase.CreateGraph<BossGraph>(editorGraphPath);

            // Force importer
            AssetDatabase.ImportAsset(
                editorGraphPath,
                ImportAssetOptions.ForceUpdate
            );
            AssetDatabase.Refresh();
            
            // Load compiled runtime asset from importer
            BossGraphAsset runtimeGraphAsset = AssetDatabase.LoadMainAssetAtPath(editorGraphPath) as BossGraphAsset;
            if (runtimeGraphAsset == null) {
                Debug.LogError(
                    $"Failed to load runtime graph at:\n{editorGraphPath}"
                );
                return;
            }

            // Assign to boss
            Undo.RecordObject(bossDirector, "Assign Boss Graph");
            bossDirector.SetGraph(runtimeGraphAsset);
            EditorUtility.SetDirty(bossDirector);
            AssetDatabase.SaveAssets();
        }
    }
}
