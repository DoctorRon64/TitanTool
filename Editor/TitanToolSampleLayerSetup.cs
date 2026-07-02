using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolSampleLayerSetup {
        private const int GroundLayerIndex = 6;
        private const string GroundLayerName = "Ground";

        static TitanToolSampleLayerSetup() {
            EditorApplication.delayCall += EnsureSampleLayers;
        }

        [MenuItem("Tools/TitanTool/Setup Sample Layers")]
        private static void EnsureSampleLayers() {
            Object[] tagManagers = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagers == null || tagManagers.Length == 0)
                return;

            SerializedObject tagManager = new(tagManagers[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty groundLayer = layers.GetArrayElementAtIndex(GroundLayerIndex);

            if (groundLayer.stringValue == GroundLayerName)
                return;

            if (!string.IsNullOrEmpty(groundLayer.stringValue)) {
                Debug.LogWarning(
                    $"TitanTool samples expect layer {GroundLayerIndex} to be named {GroundLayerName}, " +
                    $"but it is currently named {groundLayer.stringValue}. The sample ground check still uses layer {GroundLayerIndex}."
                );
                return;
            }

            groundLayer.stringValue = GroundLayerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
