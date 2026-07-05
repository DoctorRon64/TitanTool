using System.Collections.Generic;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class ShuffleBagState {
        public int selectedIndex = -1;
    }

    [NodeView("Shuffle Bag", "Composite/")]
    public class ShuffleBagNode : Node {
        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            ShuffleBagState state = ctx.GetState<ShuffleBagState>(this);
            if (state.selectedIndex < 0 || state.selectedIndex >= children.Count || children[state.selectedIndex] == null)
                state.selectedIndex = PickChildIndex(ctx);

            if (state.selectedIndex < 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            Node child = children[state.selectedIndex];
            NodeStatus result = ctx.ExecuteNode(child);
            if (result != NodeStatus.Running) {
                RememberSelection(ctx, state.selectedIndex);
                ctx.ResetBranch(child);
                ctx.ResetNode(this);
            }

            ctx.SetStatus(this, result);
            return result;
        }

        private int PickChildIndex(NodeContext ctx) {
            List<int> available = GetAvailableChildIndices();
            if (available.Count == 0)
                return -1;

            if (available.Count == 1)
                return available[0];

            List<int> remaining = GetRemainingSelections(ctx);
            remaining.RemoveAll(index => !available.Contains(index));
            if (remaining.Count == 0)
                remaining.AddRange(available);

            List<int> candidates = new(remaining);
            if (ctx.blackboard.TryGetValue(GetLastSelectionKey(), out int lastSelection) && candidates.Count > 1)
                candidates.Remove(lastSelection);

            if (candidates.Count == 0)
                candidates.AddRange(remaining);

            int selectedIndex = candidates[Random.Range(0, candidates.Count)];
            remaining.Remove(selectedIndex);
            ctx.blackboard.SetValue(GetRemainingSelectionsKey(), remaining);
            return selectedIndex;
        }

        private List<int> GetAvailableChildIndices() {
            List<int> available = new();
            for (int i = 0; i < children.Count; i++) {
                if (children[i] != null)
                    available.Add(i);
            }

            return available;
        }

        private List<int> GetRemainingSelections(NodeContext ctx) {
            string key = GetRemainingSelectionsKey();
            if (ctx.blackboard.TryGetValue(key, out List<int> remaining))
                return remaining;

            remaining = new List<int>();
            ctx.blackboard.SetValue(key, remaining);
            return remaining;
        }

        private void RememberSelection(NodeContext ctx, int selectedIndex) {
            ctx.blackboard.SetValue(GetLastSelectionKey(), selectedIndex);
        }

        private string GetRemainingSelectionsKey() => $"TitanTool.ShuffleBag.{guid}.Remaining";
        private string GetLastSelectionKey() => $"TitanTool.ShuffleBag.{guid}.Last";
    }
}
