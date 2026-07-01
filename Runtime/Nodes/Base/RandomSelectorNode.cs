using System.Collections.Generic;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class RandomSelectorState {
        public readonly List<int> order = new();
        public int currentIndex;
        public int childCount;
    }

    [NodeView("Pick Random Child", "Composite/")]
    public class RandomSelectorNode : Node {
        public override NodeStatus Tick(NodeContext ctx) {
            RandomSelectorState state = ctx.GetState<RandomSelectorState>(this);
            EnsureOrder(state);

            while (state.currentIndex < state.order.Count) {
                Node child = children[state.order[state.currentIndex]];
                if (child == null) {
                    state.currentIndex++;
                    continue;
                }

                NodeStatus result = ctx.ExecuteNode(child);

                switch (result) {
                    case NodeStatus.Running:
                        ctx.SetStatus(this, NodeStatus.Running);
                        return NodeStatus.Running;

                    case NodeStatus.Success:
                        ResetChildren(ctx);
                        ctx.ResetNode(this);
                        ctx.SetStatus(this, NodeStatus.Success);
                        return NodeStatus.Success;

                    case NodeStatus.Failure:
                        ctx.ResetBranch(child);
                        state.currentIndex++;
                        break;
                }
            }

            ResetChildren(ctx);
            ctx.ResetNode(this);
            ctx.SetStatus(this, NodeStatus.Failure);
            return NodeStatus.Failure;
        }

        private void EnsureOrder(RandomSelectorState state) {
            if (state.childCount == children.Count && state.order.Count == children.Count)
                return;

            state.order.Clear();
            state.childCount = children.Count;
            state.currentIndex = 0;

            for (int i = 0; i < children.Count; i++)
                state.order.Add(i);

            for (int i = state.order.Count - 1; i > 0; i--) {
                int swapIndex = Random.Range(0, i + 1);
                (state.order[i], state.order[swapIndex]) = (state.order[swapIndex], state.order[i]);
            }
        }

        private void ResetChildren(NodeContext ctx) {
            foreach (Node child in children)
                ctx.ResetBranch(child);
        }
    }
}
