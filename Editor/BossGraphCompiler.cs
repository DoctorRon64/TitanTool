using System;
using System.Collections.Generic;
using System.Linq;
using TitanTool.Runtime.Data;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor {
    public static class BossGraphCompiler {
        public static BossGraphCompileResult Compile(BossGraph graph) {
            TitanToolUsageLogger.LogGraphCompileAttempt(graph);

            BossGraphAsset runtime = ScriptableObject.CreateInstance<BossGraphAsset>();
            List<BossGraphValidationIssue> issues = new();
            List<RuntimeNode> runtimeNodes = new();

            if (graph == null) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, "Boss graph could not be loaded."));
                TitanToolUsageLogger.LogGraphCompileErrors(null, issues);
                return new BossGraphCompileResult(runtime, runtimeNodes, issues);
            }

            List<BossGraphNode> graphNodes = graph.GetNodes().OfType<BossGraphNode>().ToList();
            BossGraphRuntimeGuidUtility.EnsureUniqueRuntimeGuids(graphNodes);
            issues.AddRange(BossGraphValidator.Validate(graphNodes));

            List<BossGraphValidationIssue> errors = issues
                .Where(issue => issue.severity == BossGraphValidationSeverity.Error)
                .ToList();

            if (errors.Count > 0) {
                TitanToolUsageLogger.LogGraphCompileErrors(graph, errors);
                return new BossGraphCompileResult(runtime, runtimeNodes, issues);
            }

            Dictionary<string, RuntimeNode> runtimeByGuid = new();

            foreach (BossGraphNode graphNode in graphNodes) {
                RuntimeNode runtimeNode = CreateRuntimeNode(graphNode, issues);
                if (runtimeNode == null)
                    continue;

                runtimeNode.name = runtimeNode.GetType().Name;
                runtimeNode.SetGuid(graphNode.runtimeGuid);
                runtimeNode.SetViewMetadata(graphNode.displayName, graphNode.category.ToString(), graphNode.categoryColor);
                runtimeNodes.Add(runtimeNode);
                runtimeByGuid[graphNode.runtimeGuid] = runtimeNode;
            }

            foreach (BossGraphNode graphNode in graphNodes) {
                if (!runtimeByGuid.TryGetValue(graphNode.runtimeGuid, out RuntimeNode parent))
                    continue;

                foreach (BossGraphNode childGraphNode in BossGraphValidator.GetConnectedChildren(graphNode)) {
                    if (runtimeByGuid.TryGetValue(childGraphNode.runtimeGuid, out RuntimeNode child))
                        parent.children.Add(child);
                }
            }

            runtime.SetNodes(runtimeNodes);

            errors = issues
                .Where(issue => issue.severity == BossGraphValidationSeverity.Error)
                .ToList();
            TitanToolUsageLogger.LogGraphCompileErrors(graph, errors);

            return new BossGraphCompileResult(runtime, runtimeNodes, issues);
        }

        private static RuntimeNode CreateRuntimeNode(BossGraphNode graphNode, List<BossGraphValidationIssue> issues) {
            Type runtimeType = NodeTypeRegistry.GetRuntime(graphNode.GetType());
            if (runtimeType == null) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"No runtime type for {graphNode.GetType().Name}.", graphNode));
                return null;
            }

            RuntimeNode runtimeNode = ScriptableObject.CreateInstance(runtimeType) as RuntimeNode;
            if (runtimeNode == null) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"Failed creating runtime node {runtimeType.Name}.", graphNode));
                return null;
            }

            if (graphNode is IRuntimeNodeCompiler compiler)
                compiler.Compile(runtimeNode);

            return runtimeNode;
        }
    }
}
