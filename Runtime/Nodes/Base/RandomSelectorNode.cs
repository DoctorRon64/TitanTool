using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class RandomSelectorState {
        public int selectedIndex = -1;
    }

    [NodeView("Pick Random Child", "Composite/")]
    public class RandomSelectorNode : Node {
        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            RandomSelectorState state = ctx.GetState<RandomSelectorState>(this);
            if (state.selectedIndex < 0 ||
                state.selectedIndex >= children.Count ||
                children[state.selectedIndex] == null)
                state.selectedIndex = PickChildIndex();

            if (state.selectedIndex < 0) {
                ctx.ResetNode(this);
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
            int availableCount = 0;
            for (int i = 0; i < children.Count; i++) {
                if (children[i] != null)
                    availableCount++;
            }

            if (availableCount == 0)
                return -1;

            int selectedAvailableIndex = Random.Range(0, availableCount);
            for (int i = 0; i < children.Count; i++) {
                if (children[i] == null)
                    continue;

                if (selectedAvailableIndex == 0)
                    return i;

                selectedAvailableIndex--;
            }

            return -1;
        }
    }
}
