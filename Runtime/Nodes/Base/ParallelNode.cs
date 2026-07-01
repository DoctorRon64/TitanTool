using System.Collections.Generic;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public enum ParallelSuccessRule {
        AllChildren,
        AnyChild
    }

    public enum ParallelFailureRule {
        AnyChild,
        AllChildren
    }

    public class ParallelState {
        public readonly List<NodeStatus> statuses = new();
        public readonly List<bool> completed = new();
    }

    [NodeView("Run In Parallel", "Composite/")]
    public class ParallelNode : Node {
        [SerializeField] private ParallelSuccessRule m_successRule = ParallelSuccessRule.AllChildren;
        [SerializeField] private ParallelFailureRule m_failureRule = ParallelFailureRule.AnyChild;

        public void SetSuccessRule(ParallelSuccessRule rule) => m_successRule = rule;
        public void SetFailureRule(ParallelFailureRule rule) => m_failureRule = rule;

        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            ParallelState state = ctx.GetState<ParallelState>(this);
            EnsureStateSize(state);

            for (int i = 0; i < children.Count; i++) {
                if (state.completed[i])
                    continue;

                Node child = children[i];
                NodeStatus result = child != null
                    ? ctx.ExecuteNode(child)
                    : NodeStatus.Failure;

                state.statuses[i] = result;
                state.completed[i] = result != NodeStatus.Running;
            }

            int successes = 0;
            int failures = 0;
            int completed = 0;

            for (int i = 0; i < state.statuses.Count; i++) {
                if (!state.completed[i])
                    continue;

                completed++;
                if (state.statuses[i] == NodeStatus.Success)
                    successes++;
                else if (state.statuses[i] == NodeStatus.Failure)
                    failures++;
            }

            bool failureReached = m_failureRule == ParallelFailureRule.AnyChild
                ? failures > 0
                : failures == children.Count;

            bool successReached = m_successRule == ParallelSuccessRule.AnyChild
                ? successes > 0
                : successes == children.Count;

            if (failureReached)
                return Finish(ctx, NodeStatus.Failure);

            if (successReached)
                return Finish(ctx, NodeStatus.Success);

            if (completed == children.Count)
                return Finish(ctx, NodeStatus.Failure);

            ctx.SetStatus(this, NodeStatus.Running);
            return NodeStatus.Running;
        }

        private void EnsureStateSize(ParallelState state) {
            if (state.statuses.Count == children.Count &&
                state.completed.Count == children.Count) {
                return;
            }

            state.statuses.Clear();
            state.completed.Clear();

            for (int i = 0; i < children.Count; i++) {
                state.statuses.Add(NodeStatus.Running);
                state.completed.Add(false);
            }
        }

        private NodeStatus Finish(NodeContext ctx, NodeStatus status) {
            foreach (Node child in children) {
                if (child != null)
                    ctx.ResetBranch(child);
            }

            ctx.ResetNode(this);
            ctx.SetStatus(this, status);
            return status;
        }
    }
}
