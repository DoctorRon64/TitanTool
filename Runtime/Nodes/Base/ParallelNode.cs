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

    public enum ParallelBehaviorPreset {
        [InspectorName("Fail Fast - all children must succeed")]
        FailFast,
        [InspectorName("Any Success Wins - finish on first success")]
        AnySuccessWins,
        [InspectorName("Wait For All - every child finishes")]
        WaitForAll,
        [InspectorName("First Result Wins - success or failure ends it")]
        FirstResultWins
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
        public void SetPreset(ParallelBehaviorPreset preset) {
            switch (preset) {
                case ParallelBehaviorPreset.AnySuccessWins:
                    m_successRule = ParallelSuccessRule.AnyChild;
                    m_failureRule = ParallelFailureRule.AllChildren;
                    break;

                case ParallelBehaviorPreset.WaitForAll:
                    m_successRule = ParallelSuccessRule.AllChildren;
                    m_failureRule = ParallelFailureRule.AllChildren;
                    break;

                case ParallelBehaviorPreset.FirstResultWins:
                    m_successRule = ParallelSuccessRule.AnyChild;
                    m_failureRule = ParallelFailureRule.AnyChild;
                    break;

                case ParallelBehaviorPreset.FailFast:
                default:
                    m_successRule = ParallelSuccessRule.AllChildren;
                    m_failureRule = ParallelFailureRule.AnyChild;
                    break;
            }
        }

        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatusReason(this, "No connected children");
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
                ctx.SetStatusReason(child, state.completed[i]
                    ? $"Parallel child completed with {result}"
                    : "Parallel child still running");
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

            if (failureReached) {
                ctx.SetStatusReason(this, failures == 1
                    ? "Parallel finished because one child failed"
                    : $"Parallel finished because {failures} children failed");
                return Finish(ctx, NodeStatus.Failure);
            }

            if (successReached) {
                ctx.SetStatusReason(this, successes == 1
                    ? "Parallel finished because one child succeeded"
                    : "Parallel finished because all children succeeded");
                return Finish(ctx, NodeStatus.Success);
            }

            if (completed == children.Count) {
                ctx.SetStatusReason(this, "Parallel finished with mixed child results");
                return Finish(ctx, NodeStatus.Failure);
            }

            ctx.SetStatusReason(this, $"Waiting for {children.Count - completed} child branch(es)");
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
