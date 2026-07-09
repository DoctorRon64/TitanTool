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
        public readonly List<int> sequenceIndices = new();
        public readonly List<bool> stepCompleted = new();
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

            if (HasDirectSequenceChild())
                return TickLockstepSequences(ctx, state);

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

        private NodeStatus TickLockstepSequences(NodeContext ctx, ParallelState state) {
            for (int i = 0; i < children.Count; i++) {
                if (state.completed[i] || state.stepCompleted[i])
                    continue;

                if (!TryGetCurrentStep(i, state, out Node step, out bool branchFinished)) {
                    if (!branchFinished) {
                        state.statuses[i] = NodeStatus.Failure;
                        state.completed[i] = true;
                        state.stepCompleted[i] = true;
                        continue;
                    }

                    state.statuses[i] = NodeStatus.Success;
                    state.completed[i] = true;
                    state.stepCompleted[i] = true;
                    continue;
                }

                NodeStatus result = step != null
                    ? ctx.ExecuteNode(step)
                    : NodeStatus.Failure;

                state.statuses[i] = result;

                if (result == NodeStatus.Running)
                    continue;

                state.stepCompleted[i] = true;

                if (result == NodeStatus.Success) {
                    ctx.ResetBranch(step);
                } else {
                    state.completed[i] = true;
                }
            }

            if (HasReachedFailure(state))
                return Finish(ctx, NodeStatus.Failure);

            if (AllActiveBranchesFinishedStep(state))
                AdvanceLockstep(state);

            if (HasReachedFailure(state))
                return Finish(ctx, NodeStatus.Failure);

            if (HasReachedSuccess(state))
                return Finish(ctx, NodeStatus.Success);

            if (AllBranchesCompleted(state))
                return Finish(ctx, NodeStatus.Failure);

            ctx.SetStatus(this, NodeStatus.Running);
            return NodeStatus.Running;
        }

        private void EnsureStateSize(ParallelState state) {
            if (state.statuses.Count == children.Count &&
                state.completed.Count == children.Count &&
                state.sequenceIndices.Count == children.Count &&
                state.stepCompleted.Count == children.Count) {
                return;
            }

            state.statuses.Clear();
            state.completed.Clear();
            state.sequenceIndices.Clear();
            state.stepCompleted.Clear();

            for (int i = 0; i < children.Count; i++) {
                state.statuses.Add(NodeStatus.Running);
                state.completed.Add(false);
                state.sequenceIndices.Add(0);
                state.stepCompleted.Add(false);
            }
        }

        private bool HasDirectSequenceChild() {
            foreach (Node child in children) {
                if (child is SequenceNode)
                    return true;
            }

            return false;
        }

        private bool TryGetCurrentStep(int childIndex, ParallelState state, out Node step, out bool branchFinished) {
            step = null;
            branchFinished = false;

            Node branch = children[childIndex];
            if (branch == null)
                return false;

            if (branch is not SequenceNode sequence) {
                step = branch;
                return true;
            }

            int sequenceIndex = state.sequenceIndices[childIndex];
            if (sequenceIndex < 0)
                return false;

            if (sequenceIndex >= sequence.children.Count) {
                branchFinished = true;
                return false;
            }

            step = sequence.children[sequenceIndex];
            return step != null;
        }

        private bool AllActiveBranchesFinishedStep(ParallelState state) {
            for (int i = 0; i < children.Count; i++) {
                if (!state.completed[i] && !state.stepCompleted[i])
                    return false;
            }

            return true;
        }

        private void AdvanceLockstep(ParallelState state) {
            for (int i = 0; i < children.Count; i++) {
                if (state.completed[i])
                    continue;

                Node branch = children[i];
                if (branch is SequenceNode sequence) {
                    state.sequenceIndices[i]++;
                    state.stepCompleted[i] = false;

                    if (state.sequenceIndices[i] >= sequence.children.Count) {
                        state.statuses[i] = NodeStatus.Success;
                        state.completed[i] = true;
                        state.stepCompleted[i] = true;
                    }
                } else {
                    state.statuses[i] = NodeStatus.Success;
                    state.completed[i] = true;
                    state.stepCompleted[i] = true;
                }
            }
        }

        private bool HasReachedFailure(ParallelState state) {
            int failures = 0;

            for (int i = 0; i < state.statuses.Count; i++) {
                if (state.completed[i] && state.statuses[i] == NodeStatus.Failure)
                    failures++;
            }

            return m_failureRule == ParallelFailureRule.AnyChild
                ? failures > 0
                : failures == children.Count;
        }

        private bool HasReachedSuccess(ParallelState state) {
            int successes = 0;

            for (int i = 0; i < state.statuses.Count; i++) {
                if (state.completed[i] && state.statuses[i] == NodeStatus.Success)
                    successes++;
            }

            return m_successRule == ParallelSuccessRule.AnyChild
                ? successes > 0
                : successes == children.Count;
        }

        private bool AllBranchesCompleted(ParallelState state) {
            for (int i = 0; i < state.completed.Count; i++) {
                if (!state.completed[i])
                    return false;
            }

            return true;
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
