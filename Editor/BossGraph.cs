using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
        [SerializeField] private int m_lastNodeCount = -1;
        [SerializeField] private int m_lastWireCount = -1;
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

            PlaySoundForGraphDelta();
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

        private void PlaySoundForGraphDelta() {
            int nodeCount = GetNodes().Count();
            int wireCount = CountWireModels();

            if (m_lastNodeCount >= 0 && m_lastWireCount >= 0) {
                if (nodeCount > m_lastNodeCount) {
                    TitanToolEditorSoundSettings.Play(TitanToolEditorSoundEvent.NodeCreated);
                    TitanToolUsageLogger.LogNodePlaced(this, nodeCount - m_lastNodeCount, nodeCount);
                }
                else if (nodeCount < m_lastNodeCount) {
                    TitanToolEditorSoundSettings.Play(TitanToolEditorSoundEvent.NodeRemoved);
                    TitanToolUsageLogger.LogNodeRemoved(this, m_lastNodeCount - nodeCount, nodeCount);
                }
                else if (wireCount > m_lastWireCount)
                    TitanToolEditorSoundSettings.Play(TitanToolEditorSoundEvent.WireConnected);
                else if (wireCount < m_lastWireCount)
                    TitanToolEditorSoundSettings.Play(TitanToolEditorSoundEvent.WireRemoved);
            }

            m_lastNodeCount = nodeCount;
            m_lastWireCount = wireCount;
        }

        private int CountWireModels() {
            object implementation = BossGraphReflection.graphImplementationField?.GetValue(this);
            object wireModels = implementation?.GetType()
                .GetProperty("WireModels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(implementation);

            if (wireModels is ICollection collection)
                return collection.Count;

            int count = 0;
            if (wireModels is IEnumerable enumerable) {
                foreach (object _ in enumerable)
                    count++;
            }

            return count;
        }
    }

    internal static class BossGraphReflection {
        public static readonly FieldInfo graphImplementationField = typeof(Graph)
            .GetField("m_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
