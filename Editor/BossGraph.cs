using System;
using System.Linq;
using UnityEditor;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace TitanTool.Editor {
    public static class AssetPath {
        public const string ROOT = "Assets/Data";
        public const string TITAN_TOOL_FOLDER = "TitanTool";
        public const string ASSET_PATH = ROOT + "/" + TITAN_TOOL_FOLDER;
    }

    [Graph(ASSET_EXTENSION)]
    [Serializable]
    public class BossGraph : Graph {
        public const string ASSET_EXTENSION = "titan";
        [SerializeField] private bool m_rebuildQueued;
        [SerializeField] private string m_assetPath;
        public string assetPath => m_assetPath;
        public void SetAssetPath(string path) => m_assetPath = path;

        [MenuItem("Assets/Create/TitanTool/Boss Graph")]
        static void CreateAssetFile() {
            EnsureAssetFolder();
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<BossGraph>("Titan Graph");
        }

        public static void EnsureAssetFolder() {
            if (!AssetDatabase.IsValidFolder(AssetPath.ROOT))
                AssetDatabase.CreateFolder("Assets", "Data");

            if (!AssetDatabase.IsValidFolder(AssetPath.ASSET_PATH))
                AssetDatabase.CreateFolder(AssetPath.ROOT, AssetPath.TITAN_TOOL_FOLDER);
        }

        public override void OnGraphChanged(GraphLogger logger) {
            base.OnGraphChanged(logger);

            BossGraphRuntimeGuidUtility.EnsureUniqueRuntimeGuids(GetNodes().OfType<BossGraphNode>());
            CheckGraphErrors(logger);

            if (m_rebuildQueued) return;
            m_rebuildQueued = true;
            EditorApplication.delayCall += DelayedRebuild;
        }

        void CheckGraphErrors(GraphLogger logger) {
            foreach (BossGraphValidationIssue issue in BossGraphValidator.Validate(GetNodes().OfType<BossGraphNode>())) {
                if (issue.severity == BossGraphValidationSeverity.Error) {
                    logger.LogError(issue.message, issue.node != null ? issue.node : this);
                } else {
                    logger.LogWarning(issue.message, issue.node != null ? issue.node : this);
                }
            }
        }

        private void DelayedRebuild() {
            m_rebuildQueued = false;
            if (this == null)
                return;
            BossGraphSyncer.RebuildRuntime(this);
        }
    }
}
