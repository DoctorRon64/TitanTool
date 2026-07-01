using UnityEditor;
namespace TitanTool.Editor {
    public static class BossGraphSyncer {
        public static void RebuildRuntime(BossGraph graph) {
            if (graph == null) return;
            if (string.IsNullOrEmpty(graph.assetPath)) return;
            AssetDatabase.ImportAsset(graph.assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}