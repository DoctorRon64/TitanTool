using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TitanTool.Runtime;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    [CustomEditor(typeof(TargetPointProvider))]
    public class TargetPointProviderEditor : UnityEditor.Editor {
        private const string CHILD_TARGET_POINT_NAME = "Target Point";

        private GUIStyle m_cardStyle;
        private GUIStyle m_headerStyle;
        private GUIStyle m_subtleStyle;
        private GUIStyle m_statusStyle;

        public override void OnInspectorGUI() {
            EnsureStyles();
            serializedObject.Update();

            SerializedProperty points = serializedObject.FindProperty("m_points");
            DrawCard("Target Point Provider", () => {
                int childPointCount = ((TargetPointProvider)target).GetComponentsInChildren<TargetPoint>(true).Count(point => point != null);
                int scenePointCount = UnityEngine.Object.FindObjectsByType<TargetPoint>(FindObjectsSortMode.None).Count(point => point != null);

                using (new EditorGUILayout.HorizontalScope()) {
                    DrawMetric("Manual", points.arraySize.ToString());
                    DrawMetric("Children", childPointCount.ToString());
                    DrawMetric("Scene", scenePointCount.ToString());
                }

                DrawTargetPointDropZone(points);

                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Create Child Point", GUILayout.Height(24)))
                        CreateChildTargetPoint(points);

                    if (GUILayout.Button("Collect Children", GUILayout.Height(24)))
                        CollectTargetPoints(points, ((TargetPointProvider)target).GetComponentsInChildren<TargetPoint>(true), true);

                    if (GUILayout.Button("Use Scene Points", GUILayout.Height(24)))
                        CollectTargetPoints(points, UnityEngine.Object.FindObjectsByType<TargetPoint>(FindObjectsSortMode.None), true);
                }

                if (points.arraySize <= 0) {
                    DrawStatusLine("Automatic lookup", "No manual list: this provider will use TargetPoint children.", MessageType.Info);
                }
                else {
                    int missingKeyCount = CountMissingKeys(points);
                    using (new EditorGUILayout.HorizontalScope()) {
                        using (new EditorGUI.DisabledScope(missingKeyCount == 0)) {
                            string label = missingKeyCount > 0 ? $"Create Missing Target Point Keys ({missingKeyCount})" : "Create Missing Target Point Keys";
                            if (GUILayout.Button(label, GUILayout.Height(24)))
                                EnsureTargetPointKeys(GetTargetPoints(points));
                        }

                        if (GUILayout.Button("Rename Keys From Objects", GUILayout.Height(24)))
                            RenameTargetPointKeys(GetTargetPoints(points));
                    }

                    DrawTargetPointRows(points);
                }
            });

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTargetPointRows(SerializedProperty points) {
            HashSet<TargetPointKey> seenKeys = new();
            bool foundDuplicate = false;

            for (int i = 0; i < points.arraySize; i++) {
                SerializedProperty element = points.GetArrayElementAtIndex(i);
                TargetPoint point = element.objectReferenceValue as TargetPoint;
                TargetPointKey key = point != null ? point.key : null;

                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField(i.ToString(), m_subtleStyle, GUILayout.Width(18));

                    EditorGUI.BeginChangeCheck();
                    TargetPoint newPoint = EditorGUILayout.ObjectField(point, typeof(TargetPoint), true) as TargetPoint;
                    if (EditorGUI.EndChangeCheck()) {
                        element.objectReferenceValue = newPoint;
                        if (newPoint != null)
                            EnsureTargetPointKeys(new[] { newPoint });
                    }

                    using (new EditorGUI.DisabledScope(point == null)) {
                        EditorGUI.BeginChangeCheck();
                        TargetPointKey newKey = EditorGUILayout.ObjectField(key, typeof(TargetPointKey), false, GUILayout.Width(140)) as TargetPointKey;
                        if (EditorGUI.EndChangeCheck())
                            SetTargetPointKey(point, newKey);

                        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                            Selection.activeObject = point;

                        if (GUILayout.Button("Rename Key", EditorStyles.miniButton, GUILayout.Width(78)))
                            RenameTargetPointKey(point);
                    }

                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(58))) {
                        points.DeleteArrayElementAtIndex(i);
                        i--;
                        continue;
                    }
                }

                if (point != null && key == null)
                    DrawStatusLine("Missing key", $"{point.name} has no TargetPointKey.", MessageType.Warning);
                else if (point != null && key != null && !TargetPointKeyMatchesPointName(point))
                    DrawStatusLine("Rename suggested", $"Key asset is '{key.name}', object is '{point.name}'.", MessageType.Info);

                if (key != null && !seenKeys.Add(key))
                    foundDuplicate = true;
            }

            if (foundDuplicate)
                DrawStatusLine("Duplicate keys", "TargetPoint lookup will use the first point with each duplicated key.", MessageType.Warning);
        }

        private void DrawTargetPointDropZone(SerializedProperty points) {
            Rect rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop scene objects or TargetPoint components here", EditorStyles.helpBox);

            Event current = Event.current;
            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
                return;

            bool hasValidObject = DragAndDrop.objectReferences.Any(CanConvertToTargetPoint);
            DragAndDrop.visualMode = hasValidObject ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (current.type == EventType.DragPerform && hasValidObject) {
                DragAndDrop.AcceptDrag();
                foreach (UnityEngine.Object droppedObject in DragAndDrop.objectReferences) {
                    TargetPoint point = GetOrCreateTargetPoint(droppedObject);
                    if (point != null)
                        AddTargetPoint(points, point);
                }
            }

            current.Use();
        }

        private void CreateChildTargetPoint(SerializedProperty points) {
            TargetPointProvider provider = (TargetPointProvider)target;
            GameObject pointObject = new(GetNextChildTargetPointName(provider.transform));
            Undo.RegisterCreatedObjectUndo(pointObject, "Create Target Point");
            Undo.SetTransformParent(pointObject.transform, provider.transform, "Parent Target Point");
            pointObject.transform.localPosition = Vector3.zero;

            TargetPoint point = pointObject.AddComponent<TargetPoint>();
            EnsureTargetPointKeys(new[] { point });
            AddTargetPoint(points, point);
            Selection.activeObject = pointObject;
        }

        private static string GetNextChildTargetPointName(Transform parent) {
            HashSet<int> usedNumbers = new();
            foreach (TargetPoint point in parent.GetComponentsInChildren<TargetPoint>(true)) {
                if (point == null)
                    continue;

                string pointName = point.name.Trim();
                if (pointName == CHILD_TARGET_POINT_NAME) {
                    usedNumbers.Add(1);
                    continue;
                }

                string prefix = $"{CHILD_TARGET_POINT_NAME} ";
                if (!pointName.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                string numberText = pointName.Substring(prefix.Length).Split(' ')[0];
                if (int.TryParse(numberText, out int number) && number > 0)
                    usedNumbers.Add(number);
            }

            int nextNumber = 1;
            while (usedNumbers.Contains(nextNumber))
                nextNumber++;

            return $"{CHILD_TARGET_POINT_NAME} {nextNumber} {Guid.NewGuid():N}";
        }

        private void CollectTargetPoints(SerializedProperty points, IEnumerable<TargetPoint> targetPoints, bool createMissingKeys) {
            Undo.RecordObject(target, "Collect Target Points");

            TargetPoint[] orderedTargetPoints = targetPoints
                .Where(point => point != null)
                .OrderBy(point => point.key != null ? point.key.name : point.name)
                .ThenBy(point => point.name)
                .ToArray();

            if (createMissingKeys)
                EnsureTargetPointKeys(orderedTargetPoints);

            points.arraySize = orderedTargetPoints.Length;
            for (int i = 0; i < orderedTargetPoints.Length; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = orderedTargetPoints[i];

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static bool CanConvertToTargetPoint(UnityEngine.Object value) {
            return value is TargetPoint ||
                   value is GameObject ||
                   value is Component;
        }

        private static TargetPoint GetOrCreateTargetPoint(UnityEngine.Object value) {
            if (value is TargetPoint point)
                return point;

            GameObject gameObject = value switch {
                GameObject go => go,
                Component component => component.gameObject,
                _ => null
            };

            if (gameObject == null)
                return null;

            if (gameObject.TryGetComponent(out TargetPoint existingPoint))
                return existingPoint;

            return Undo.AddComponent<TargetPoint>(gameObject);
        }

        private static void AddTargetPoint(SerializedProperty points, TargetPoint point) {
            if (point == null)
                return;

            EnsureTargetPointKeys(new[] { point });

            for (int i = 0; i < points.arraySize; i++) {
                if (points.GetArrayElementAtIndex(i).objectReferenceValue == point)
                    return;
            }

            int index = points.arraySize;
            points.arraySize++;
            points.GetArrayElementAtIndex(index).objectReferenceValue = point;
        }

        private static IEnumerable<TargetPoint> GetTargetPoints(SerializedProperty points) {
            for (int i = 0; i < points.arraySize; i++) {
                if (points.GetArrayElementAtIndex(i).objectReferenceValue is TargetPoint point)
                    yield return point;
            }
        }

        private static int CountMissingKeys(SerializedProperty points) {
            int count = 0;
            for (int i = 0; i < points.arraySize; i++) {
                if (points.GetArrayElementAtIndex(i).objectReferenceValue is TargetPoint point && point.key == null)
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

                SetTargetPointKey(point, key);
            }

            AssetDatabase.SaveAssets();
        }

        private static void RenameTargetPointKeys(IEnumerable<TargetPoint> targetPoints) {
            foreach (TargetPoint point in targetPoints)
                RenameTargetPointKey(point);

            AssetDatabase.SaveAssets();
        }

        private static void RenameTargetPointKey(TargetPoint point) {
            if (point == null || point.key == null)
                return;

            string path = AssetDatabase.GetAssetPath(point.key);
            if (string.IsNullOrEmpty(path))
                return;

            string safeName = MakeSafeFileName(point.name);
            if (point.key.name == safeName && Path.GetFileNameWithoutExtension(path) == safeName)
                return;

            Undo.RecordObject(point.key, "Rename Target Point Key");
            point.key.name = safeName;
            EditorUtility.SetDirty(point.key);

            string error = AssetDatabase.RenameAsset(path, safeName);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"Could not rename TargetPointKey '{path}' to '{safeName}': {error}", point.key);
        }

        private static bool TargetPointKeyMatchesPointName(TargetPoint point) {
            return point != null &&
                   point.key != null &&
                   point.key.name == MakeSafeFileName(point.name);
        }

        private static void SetTargetPointKey(TargetPoint point, TargetPointKey key) {
            if (point == null)
                return;

            Undo.RecordObject(point, "Set Target Point Key");
            SerializedObject pointObject = new(point);
            pointObject.FindProperty("m_key").objectReferenceValue = key;
            pointObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(point);
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

        private static string MakeSafeFileName(string fileName) {
            char[] invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct().ToArray();
            string safeName = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(safeName) ? "TargetPoint" : safeName.Trim();
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
