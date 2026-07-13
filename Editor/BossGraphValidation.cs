using System;
using System.Collections.Generic;
using System.Linq;
using TitanTool.Runtime;
using TitanTool.Runtime.Values;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using RuntimeStartNode = TitanTool.Runtime.Nodes.Base.StartNode;

namespace TitanTool.Editor {
    public enum BossGraphValidationSeverity {
        Error,
        Warning
    }

    public readonly struct BossGraphValidationIssue {
        public BossGraphValidationIssue(BossGraphValidationSeverity severity, string message, object node = null) {
            this.severity = severity;
            this.message = message;
            this.node = node;
        }

        public readonly BossGraphValidationSeverity severity;
        public readonly string message;
        public readonly object node;
    }

    public sealed class BossGraphNodeValidationContext {
        private readonly List<BossGraphValidationIssue> m_issues;

        public BossGraphNodeValidationContext(BossGraphNode node, List<BossGraphValidationIssue> issues) {
            this.node = node;
            m_issues = issues;
        }

        public BossGraphNode node { get; }

        public T GetInputValue<T>(string portName) {
            return GraphNodePortUtility.GetInputValue<T>(node, portName);
        }

        public void Error(string message) {
            m_issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, message, node));
        }

        public void Warning(string message) {
            m_issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Warning, message, node));
        }

        public void ValidateTargetPointKey(string portName, string label) {
            TargetPointKey key = GetInputValue<TargetPointKey>(portName);
            if (key == null)
                return;

            if (!TargetPointKeySceneLookup.Contains(key)) {
                Warning($"{label} uses TargetPointKey '{key.name}', but no TargetPoint in the current scene/provider uses that key.");
            }
        }
    }

    public interface IGraphNodeValidator {
        void Validate(BossGraphNodeValidationContext context);
    }

    public sealed class GraphValueNodeValidationContext {
        private readonly INode m_node;
        private readonly List<BossGraphValidationIssue> m_issues;

        public GraphValueNodeValidationContext(INode node, List<BossGraphValidationIssue> issues) {
            m_node = node;
            m_issues = issues;
        }

        public T GetInputValue<T>(string portName) {
            return GraphNodePortUtility.GetInputValue<T>(m_node, portName);
        }

        public void Error(string message) {
            m_issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, message, m_node));
        }

        public void Warning(string message) {
            m_issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Warning, message, m_node));
        }

        public void ValidateTargetPointKey(string portName, string label) {
            TargetPointKey key = GetInputValue<TargetPointKey>(portName);
            if (key == null)
                return;

            if (!TargetPointKeySceneLookup.Contains(key))
                Warning($"{label} uses TargetPointKey '{key.name}', but no TargetPoint in the current scene/provider uses that key.");
        }
    }

    public interface IGraphValueNodeValidator {
        void Validate(GraphValueNodeValidationContext context);
    }

    public static class BossGraphValidator {
        public static List<BossGraphValidationIssue> Validate(IEnumerable<INode> nodes) {
            List<INode> allNodes = nodes.ToList();
            List<BossGraphNode> graphNodes = allNodes.OfType<BossGraphNode>().ToList();
            List<BossGraphValidationIssue> issues = new();

            if (graphNodes.Count == 0) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, "Boss graph has no nodes."));
                return issues;
            }

            ValidateStartNode(graphNodes, issues);
            HashSet<BossGraphNode> executableNodes = GetExecutableNodes(graphNodes);
            if (executableNodes.Count == 0)
                return issues;

            ValidateNodeIdentity(executableNodes.ToList(), issues);
            ValidateExecutionOutputConnections(executableNodes.ToList(), issues);
            ValidateConnectivity(graphNodes, executableNodes, issues);
            ValidateCycles(executableNodes.ToList(), issues);
            ValidateNodeRules(executableNodes.ToList(), issues);
            ValidateValueNodeRules(allNodes, issues);

            return issues;
        }

        public static HashSet<BossGraphNode> GetExecutableNodes(IEnumerable<BossGraphNode> nodes) {
            List<BossGraphNode> graphNodes = nodes.ToList();
            BossGraphNode startNode = graphNodes.FirstOrDefault(n => NodeTypeRegistry.GetRuntime(n.GetType()) == typeof(RuntimeStartNode));
            if (startNode == null)
                return new HashSet<BossGraphNode>();

            HashSet<BossGraphNode> reachable = new();
            Stack<BossGraphNode> stack = new();
            stack.Push(startNode);

            while (stack.Count > 0) {
                BossGraphNode node = stack.Pop();
                if (!reachable.Add(node))
                    continue;

                foreach (BossGraphNode child in GetConnectedChildren(node)) {
                    if (child != null)
                        stack.Push(child);
                }
            }

            return reachable;
        }

        public static IEnumerable<BossGraphNode> GetConnectedChildren(BossGraphNode node) {
            INode iNode = node;
            for (int i = 0; i < iNode.outputPortCount; i++) {
                IPort port = iNode.GetOutputPort(i);
                if (port == null) {
                    continue;
                }

                List<IPort> connected = new();
                port.GetConnectedPorts(connected);

                foreach (IPort connectedPort in connected) {
                    if (connectedPort.GetNode() is BossGraphNode child) {
                        yield return child;
                    }
                }
            }
        }

        private static void ValidateNodeIdentity(List<BossGraphNode> graphNodes, List<BossGraphValidationIssue> issues) {
            foreach (BossGraphNode node in graphNodes) {
                if (string.IsNullOrEmpty(node.runtimeGuid)) {
                    issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"{node.GetType().Name} is missing a runtime GUID.", node));
                } else if (!Guid.TryParse(node.runtimeGuid, out _)) {
                    issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"{node.GetType().Name} has an invalid runtime GUID: {node.runtimeGuid}", node));
                }

                if (NodeTypeRegistry.GetRuntime(node.GetType()) == null) {
                    issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"No runtime type is registered for {node.GetType().Name}.", node));
                }
            }

            foreach (IGrouping<string, BossGraphNode> duplicateGuid in graphNodes
                         .Where(n => !string.IsNullOrEmpty(n.runtimeGuid))
                         .GroupBy(n => n.runtimeGuid)
                         .Where(g => g.Count() > 1)) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"Duplicate runtime GUID in graph: {duplicateGuid.Key}", duplicateGuid.First()));
            }
        }

        private static void ValidateStartNode(List<BossGraphNode> graphNodes, List<BossGraphValidationIssue> issues) {
            List<BossGraphNode> startNodes = graphNodes
                .Where(n => NodeTypeRegistry.GetRuntime(n.GetType()) == typeof(RuntimeStartNode))
                .ToList();

            if (startNodes.Count != 1) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"Boss graph must contain exactly one Start node, but found {startNodes.Count}."));
                return;
            }

            if (!GetConnectedChildren(startNodes[0]).Any()) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, "Start node must be connected to at least one child node.", startNodes[0]));
            }
        }

        private static void ValidateConnectivity(List<BossGraphNode> graphNodes, HashSet<BossGraphNode> executableNodes, List<BossGraphValidationIssue> issues) {
            foreach (BossGraphNode node in graphNodes.Where(n => NodeTypeRegistry.GetRuntime(n.GetType()) != typeof(RuntimeStartNode))) {
                if (!executableNodes.Contains(node)) {
                    issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Warning, $"{node.GetType().Name} is not connected to Start and will be ignored at runtime.", node));
                }
            }
        }

        private static void ValidateExecutionOutputConnections(List<BossGraphNode> graphNodes, List<BossGraphValidationIssue> issues) {
            foreach (BossGraphNode node in graphNodes) {
                INode iNode = node;
                for (int i = 0; i < iNode.outputPortCount; i++) {
                    IPort port = iNode.GetOutputPort(i);
                    if (port == null || !IsExecutionOutputPortName(port.name))
                        continue;

                    List<IPort> connected = new();
                    port.GetConnectedPorts(connected);
                    if (connected.Count > 1) {
                        issues.Add(new BossGraphValidationIssue(
                            BossGraphValidationSeverity.Error,
                            $"{node.GetType().Name} output {port.name} can only connect to one child. Use another output port for another child.",
                            node));
                    }
                }
            }
        }

        private static bool IsExecutionOutputPortName(string portName) {
            return portName != null &&
                   portName.StartsWith("Out", StringComparison.Ordinal) &&
                   int.TryParse(portName["Out".Length..], out _);
        }

        private static void ValidateCycles(List<BossGraphNode> graphNodes, List<BossGraphValidationIssue> issues) {
            Dictionary<BossGraphNode, int> states = new();

            foreach (BossGraphNode node in graphNodes) {
                if (Visit(node)) {
                    issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, "Boss graph contains a cycle. Runtime behavior trees must be acyclic.", node));
                    return;
                }
            }

            bool Visit(BossGraphNode node) {
                if (states.TryGetValue(node, out int state)) {
                    return state == 1;
                }

                states[node] = 1;

                foreach (BossGraphNode child in GetConnectedChildren(node)) {
                    if (Visit(child)) {
                        return true;
                    }
                }

                states[node] = 2;
                return false;
            }
        }

        private static void ValidateNodeRules(List<BossGraphNode> graphNodes, List<BossGraphValidationIssue> issues) {
            foreach (BossGraphNode node in graphNodes) {
                if (node is IGraphNodeValidator validator) {
                    validator.Validate(new BossGraphNodeValidationContext(node, issues));
                }
            }
        }

        private static void ValidateValueNodeRules(List<INode> nodes, List<BossGraphValidationIssue> issues) {
            foreach (INode node in nodes) {
                if (node is IGraphValueNodeValidator validator)
                    validator.Validate(new GraphValueNodeValidationContext(node, issues));
            }
        }
    }

    public static class TargetPointKeySceneLookup {
        public static bool Contains(TargetPointKey key) {
            if (key == null)
                return false;

            foreach (TargetPoint point in Resources.FindObjectsOfTypeAll<TargetPoint>()) {
                if (point == null ||
                    point.key != key ||
                    point.gameObject == null ||
                    !point.gameObject.scene.IsValid()) {
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    public static class BossGraphRuntimeGuidUtility {
        public static void EnsureUniqueRuntimeGuids(IEnumerable<BossGraphNode> nodes) {
            HashSet<string> usedGuids = new();

            foreach (BossGraphNode node in nodes) {
                if (node == null)
                    continue;

                if (string.IsNullOrEmpty(node.runtimeGuid) ||
                    !Guid.TryParse(node.runtimeGuid, out _) ||
                    !usedGuids.Add(node.runtimeGuid)) {
                    do {
                        node.RegenerateRuntimeGuid();
                    } while (!usedGuids.Add(node.runtimeGuid));
                }
            }
        }
    }

    public interface IGraphValueProvider {
        bool TryGetValue<T>(out T value);
    }

    public readonly struct GraphRandomRange<T> {
        public GraphRandomRange(T min, T max) {
            this.min = min;
            this.max = max;
        }

        public readonly T min;
        public readonly T max;
    }

    public interface IGraphRandomRangeProvider {
        bool TryGetRange<T>(out GraphRandomRange<T> range);
    }

    public interface IGraphTargetPointKeySetProvider {
        bool TryGetTargetPointKeys(out TargetPointKey[] keys);
    }

    public static class GraphNodePortUtility {
        public static T GetInputValue<T>(BossGraphNode node, string portName) {
            return TryGetInputValue(node, portName, out T value) ? value : default;
        }

        public static T GetInputValue<T>(INode node, string portName) {
            return TryGetInputValue(node, portName, out T value) ? value : default;
        }

        public static bool TryGetInputValue<T>(BossGraphNode node, string portName, out T value) {
            return TryGetInputValue((INode)node, portName, out value);
        }

        public static bool TryGetInputValue<T>(INode node, string portName, out T value) {
            value = default;

            if (node == null) {
                return false;
            }

            IPort port = FindInputPortByName(node, portName);
            if (port == null) {
                return false;
            }

            List<IPort> connectedPorts = new();
            port.GetConnectedPorts(connectedPorts);

            foreach (IPort connectedPort in connectedPorts) {
                if (connectedPort.GetNode() is IGraphValueProvider valueProvider &&
                    valueProvider.TryGetValue(out value)) {
                    return true;
                }

                if (connectedPort.TryGetValue(out value)) {
                    return true;
                }

                if (connectedPort.GetNode() is IVariableNode variableNode &&
                    variableNode.variable != null &&
                    variableNode.variable.TryGetDefaultValue(out value)) {
                    return true;
                }
            }

            if (port.TryGetValue(out value)) {
                return true;
            }

            return false;
        }

        public static RuntimeFloatValue GetRuntimeFloatValue(INode node, string portName) {
            if (TryGetInputRandomRange(node, portName, out GraphRandomRange<float> range))
                return RuntimeFloatValue.RandomRange(range.min, range.max);

            return RuntimeFloatValue.Fixed(GetInputValue<float>(node, portName));
        }

        public static RuntimeIntValue GetRuntimeIntValue(INode node, string portName) {
            if (TryGetInputRandomRange(node, portName, out GraphRandomRange<int> range))
                return RuntimeIntValue.RandomRange(range.min, range.max);

            return RuntimeIntValue.Fixed(GetInputValue<int>(node, portName));
        }

        public static RuntimeVector2Value GetRuntimeVector2Value(INode node, string portName) {
            if (TryGetInputRandomRange(node, portName, out GraphRandomRange<Vector2> range))
                return RuntimeVector2Value.RandomRange(range.min, range.max);

            return RuntimeVector2Value.Fixed(GetInputValue<Vector2>(node, portName));
        }

        public static RuntimeTargetPointKeyValue GetRuntimeTargetPointKeyValue(INode node, string portName) {
            if (TryGetInputTargetPointKeys(node, portName, out TargetPointKey[] keys))
                return RuntimeTargetPointKeyValue.Random(keys);

            return RuntimeTargetPointKeyValue.Fixed(GetInputValue<TargetPointKey>(node, portName));
        }

        public static bool TryGetInputRandomRange<T>(INode node, string portName, out GraphRandomRange<T> range) {
            range = default;

            if (node == null)
                return false;

            IPort port = FindInputPortByName(node, portName);
            if (port == null)
                return false;

            List<IPort> connectedPorts = new();
            port.GetConnectedPorts(connectedPorts);

            foreach (IPort connectedPort in connectedPorts) {
                if (connectedPort.GetNode() is IGraphRandomRangeProvider rangeProvider &&
                    rangeProvider.TryGetRange(out range)) {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInputTargetPointKeys(INode node, string portName, out TargetPointKey[] keys) {
            keys = null;

            if (node == null)
                return false;

            IPort port = FindInputPortByName(node, portName);
            if (port == null)
                return false;

            List<IPort> connectedPorts = new();
            port.GetConnectedPorts(connectedPorts);

            foreach (IPort connectedPort in connectedPorts) {
                if (connectedPort.GetNode() is IGraphTargetPointKeySetProvider keySetProvider &&
                    keySetProvider.TryGetTargetPointKeys(out keys)) {
                    return true;
                }
            }

            return false;
        }

        private static IPort FindInputPortByName(INode node, string portName) {
            foreach (IPort inputPort in node.GetInputPorts()) {
                if (inputPort != null && inputPort.name == portName) {
                    return inputPort;
                }
            }

            return null;
        }
    }
}
