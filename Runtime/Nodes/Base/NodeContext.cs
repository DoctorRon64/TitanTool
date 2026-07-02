using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace TitanTool.Runtime.Nodes.Base {
    public class RuntimeNodeDebugData {
        public NodeStatus status;
        public float executionTime;
        public bool visited;
    }
    
    public class NodeContext {
        public readonly Blackboard blackboard = new();
        public IReadOnlyDictionary<Node, float> timings => m_timings;
        
        private readonly Dictionary<Node, object> m_state = new();
        private readonly Dictionary<Node, NodeStatus> m_status = new();
        private readonly Signal<Node, NodeStatus> m_onNodeStatusChanged = new();
        private readonly Dictionary<Node, float> m_timings = new();
        private readonly List<Node> m_executionStack = new();
            
        public float deltaTime;
        public bool debugLogging;
        
        private readonly Dictionary<Node, RuntimeNodeDebugData> m_debug = new();
        public RuntimeNodeDebugData GetDebug(Node node) {
            if (m_debug.TryGetValue(node, out RuntimeNodeDebugData data)) return data;
            data = new RuntimeNodeDebugData();
            m_debug[node] = data;
            return data;
        }

        public void RecordTiming(Node node, float time) {
            m_timings[node] = time;
        }

        public float GetTiming(Node node) {
            return m_timings.GetValueOrDefault(node, 0f);
        }

        private readonly List<Node> m_lastTickPath = new();
        public IReadOnlyList<Node> lastTickPath => m_lastTickPath;

        public void BeginFrame() {
            m_lastTickPath.Clear();

            foreach (RuntimeNodeDebugData data in m_debug.Values) {
                data.visited = false;
            }
        }

        public void RecordVisited(Node node) {
            m_lastTickPath.Add(node);
        }

        public NodeStatus ExecuteNode(Node node) {
            if (node == null) {
                Debug.Log($"<color=red>{nameof(node)} is null</color>");
                return NodeStatus.Failure;
            }
            RecordVisited(node);
            float start = Time.realtimeSinceStartup;
            m_executionStack.Add(node);

            try {
                NodeStatus result = node.Tick(this);
                float duration = Time.realtimeSinceStartup - start;
                RecordTiming(node, duration);
                if (debugLogging) {
                    Debug.Log($"<color=yellow>[{node.name}] </color> finished with {result} " + $"({duration * 1000f:F2} ms)");
                }
                RuntimeNodeDebugData debug = GetDebug(node);
                debug.visited = true;
                debug.status = result;
                debug.executionTime = duration;
                return result;
            }
            finally {
                m_executionStack.RemoveAt(m_executionStack.Count - 1);
            }
        }

        public T GetState<T>(Node node) where T : class, new() {
            if (m_state.TryGetValue(node, out object value)) {
                return value as T;
            }

            value = new T();
            m_state[node] = value;
            return (T)value;
        }

        public void SetStatus(Node node, NodeStatus status) {
            if (m_status.TryGetValue(node, out NodeStatus current)) {
                if (current == status) {
                    return;
                }
            }

            m_status[node] = status;
            m_onNodeStatusChanged?.Invoke(node, status);
        }

        public NodeStatus GetStatus(Node node) => m_status.GetValueOrDefault(node, NodeStatus.Failure);
        public bool HasState(Node node) => m_state.ContainsKey(node);
        public void ResetNode(Node node) => m_state.Remove(node);
        public void AbortNode(Node node) {
            if (node == null)
                return;

            node.Abort(this);
            ResetNode(node);
            SetStatus(node, NodeStatus.Failure);
        }

        public int AbortRunningNodes(Node except = null) {
            List<Node> runningNodes = new();

            foreach (KeyValuePair<Node, NodeStatus> entry in m_status) {
                if (entry.Value != NodeStatus.Running || entry.Key == except || IsExecuting(entry.Key))
                    continue;

                runningNodes.Add(entry.Key);
            }

            HashSet<Node> visited = new();
            foreach (Node node in runningNodes) {
                AbortBranch(node, visited, except);
            }

            return visited.Count;
        }

        public void AbortBranch(Node node) {
            AbortBranch(node, new HashSet<Node>(), null);
        }

        public void ResetBranch(Node node) {
            if (node == null)
                return;

            HashSet<Node> visited = new();
            Traverse(node);
            return;

            void Traverse(Node current) {
                if (!visited.Add(current))
                    return;

                if (GetStatus(current) == NodeStatus.Running && !IsExecuting(current)) {
                    AbortNode(current);
                } else {
                    ResetNode(current);
                }

                foreach (Node child in current.children) {
                    if (child != null)
                        Traverse(child);
                }
            }
        }

        private void AbortBranch(Node node, HashSet<Node> visited, Node except) {
            if (node == null || node == except || IsExecuting(node) || !visited.Add(node))
                return;

            AbortNode(node);

            foreach (Node child in node.children) {
                AbortBranch(child, visited, except);
            }
        }

        private bool IsExecuting(Node node) {
            return m_executionStack.Contains(node);
        }

        public void ResetAll() {
            m_state.Clear();
            m_status.Clear();
        }
    }
}
