using System.Collections.Generic;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class WeightedRandomSelectorState {
        public int selectedIndex = -1;
    }

    [NodeView("Pick Weighted Child", "Composite/")]
    public class WeightedRandomSelectorNode : Node {
        [SerializeField] private List<RuntimeFloatValue> m_weights = new();

        public void SetWeights(IEnumerable<float> weights) {
            m_weights = new List<RuntimeFloatValue>();
            if (weights == null)
                return;

            foreach (float weight in weights)
                m_weights.Add(RuntimeFloatValue.Fixed(Mathf.Max(0f, weight)));
        }

        public void SetWeights(IEnumerable<RuntimeFloatValue> weights) {
            m_weights = new List<RuntimeFloatValue>();
            if (weights == null)
                return;

            m_weights.AddRange(weights);
        }

        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            WeightedRandomSelectorState state = ctx.GetState<WeightedRandomSelectorState>(this);
            if (state.selectedIndex < 0 || state.selectedIndex >= children.Count || children[state.selectedIndex] == null)
                state.selectedIndex = PickChildIndex();

            if (state.selectedIndex < 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            Node child = children[state.selectedIndex];
            NodeStatus result = ctx.ExecuteNode(child);
            if (result != NodeStatus.Running) {
                ctx.ResetBranch(child);
                ctx.ResetNode(this);
            }

            ctx.SetStatus(this, result);
            return result;
        }

        private int PickChildIndex() {
            List<float> weights = new();
            float total = 0f;
            for (int i = 0; i < children.Count; i++) {
                float weight = children[i] != null ? GetWeight(i) : 0f;
                weights.Add(weight);
                total += weight;
            }

            if (total <= 0f)
                return FirstConnectedChildIndex();

            float roll = Random.Range(0f, total);
            for (int i = 0; i < children.Count; i++) {
                if (children[i] == null)
                    continue;

                roll -= weights[i];
                if (roll <= 0f)
                    return i;
            }

            return FirstConnectedChildIndex();
        }

        private float GetWeight(int index) {
            return index >= 0 && index < m_weights.Count ? Mathf.Max(0f, m_weights[index].Evaluate()) : 1f;
        }

        private int FirstConnectedChildIndex() {
            for (int i = 0; i < children.Count; i++) {
                if (children[i] != null)
                    return i;
            }

            return -1;
        }
    }
}
