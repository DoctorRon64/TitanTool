using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TitanTool.Editor.Nodes;
using UnityEditor;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using RuntimeStartNode = TitanTool.Runtime.Nodes.Base.StartNode;

namespace TitanTool.Editor {
    public static class AssetPath {
        public const string ROOT = "Assets/Data";
        public const string TITAN_TOOL_FOLDER = "TitanTool";
        public const string ASSET_PATH = ROOT + "/" + TITAN_TOOL_FOLDER;
    }

    [Graph(ASSET_EXTENSION, GraphOptions.DisableAutoInclusionOfNodesFromGraphAssembly)]
    [Serializable]
    public class BossGraph : Graph {
        public const string ASSET_EXTENSION = "titan";
        [SerializeField] private bool m_rebuildQueued;
        [SerializeField] private string m_assetPath;
        [SerializeField] private int m_lastNodeCount = -1;
        [SerializeField] private int m_lastWireCount = -1;
        [NonSerialized] private bool m_ensuringStartNode;
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

        public override void OnEnable() {
            base.OnEnable();
            EditorApplication.delayCall += EnsureStartNodeDelayed;
        }

        public override void OnGraphChanged(GraphLogger logger) {
            base.OnGraphChanged(logger);

            PlaySoundForGraphDelta();
            EnsureStartNode();
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

        private void EnsureStartNodeDelayed() {
            if (this == null)
                return;

            EnsureStartNode();
        }

        private bool EnsureStartNode() {
            if (m_ensuringStartNode)
                return false;

            m_ensuringStartNode = true;
            try {
                BossGraphNode[] startNodes = GetNodes()
                    .OfType<BossGraphNode>()
                    .Where(node => NodeTypeRegistry.GetRuntime(node.GetType()) == typeof(RuntimeStartNode))
                    .ToArray();

                foreach (BossGraphNode startNode in startNodes) {
                    StartNodeLockUtility.Apply(startNode);
                }

                if (startNodes.Length > 1)
                    return RemoveDuplicateStartNodes(startNodes);

                if (startNodes.Length > 0)
                    return false;

                return CreateStartNode();
            }
            finally {
                m_ensuringStartNode = false;
            }
        }

        private bool RemoveDuplicateStartNodes(BossGraphNode[] startNodes) {
            object graphModel = BossGraphReflection.graphImplementationField?.GetValue(this);
            if (graphModel == null)
                return false;

            Array duplicateModels = GetDuplicateStartNodeModels(graphModel, startNodes.Skip(1));
            if (duplicateModels == null || duplicateModels.Length == 0)
                return false;

            MethodInfo deleteElements = graphModel.GetType().GetMethod("DeleteElements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (deleteElements == null)
                return false;

            try {
                deleteElements.Invoke(graphModel, new object[] { duplicateModels });
                return true;
            }
            catch (TargetInvocationException exception) {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            }
            catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }
        }

        private static Array GetDuplicateStartNodeModels(object graphModel, IEnumerable<BossGraphNode> duplicateStartNodes) {
            Type graphElementModelType = FindType("Unity.GraphToolkit.Editor.GraphElementModel");
            if (graphElementModelType == null)
                return null;

            ArrayList models = new();
            object rawNodeModels = graphModel.GetType()
                .GetProperty("NodeModels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(graphModel);

            if (rawNodeModels is IEnumerable nodeModels) {
                ArrayList nodeModelList = new();
                foreach (object nodeModel in nodeModels)
                    nodeModelList.Add(nodeModel);

                foreach (BossGraphNode duplicateStartNode in duplicateStartNodes) {
                    object model = FindNodeModel(nodeModelList, duplicateStartNode);
                    if (model != null && graphElementModelType.IsInstanceOfType(model))
                        models.Add(model);
                }
            }

            Array array = Array.CreateInstance(graphElementModelType, models.Count);
            for (int i = 0; i < models.Count; i++)
                array.SetValue(models[i], i);

            return array;
        }

        private static object FindNodeModel(IEnumerable nodeModels, BossGraphNode node) {
            foreach (object nodeModel in nodeModels) {
                object modelNode = nodeModel.GetType()
                    .GetProperty("Node", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(nodeModel);

                if (ReferenceEquals(modelNode, node))
                    return nodeModel;
            }

            return null;
        }

        private bool CreateStartNode() {
            object graphModel = BossGraphReflection.graphImplementationField?.GetValue(this);
            if (graphModel == null)
                return false;

            MethodInfo createNode = FindCreateNodeModelMethod(graphModel.GetType());
            if (createNode == null)
                return false;

            StartNode startNode = new();
            startNode.InitializeNode(typeof(RuntimeStartNode));

            try {
                object createdModel = createNode.Invoke(graphModel, new object[] { startNode, new Vector2(-360f, 0f) });
                StartNodeLockUtility.Apply(startNode);
                return createdModel != null;
            }
            catch (TargetInvocationException exception) {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            }
            catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }
        }

        private static MethodInfo FindCreateNodeModelMethod(Type graphModelType) {
            foreach (MethodInfo method in graphModelType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (method.Name != "CreateNodeModel")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(Vector2))
                    return method;
            }

            return null;
        }

        private static Type FindType(string fullName) {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }

    internal static class BossGraphReflection {
        public static readonly FieldInfo graphImplementationField = typeof(Graph)
            .GetField("m_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
