using TitanTool.Runtime.Values;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public enum RepeaterCompletionMode {
        [InspectorName("Restart When Re-entered")]
        RestartWhenReentered,
        [InspectorName("Remember Success")]
        RememberSuccess
    }

    public class RepeaterState {
        public bool initialized;
        public bool completed;
        public int completedRepeats;
        public int currentIndex;
        public int repeatCount;
    }

    [NodeView("Repeat Sequence", "Composite/")]
    public class RepeaterNode : Node {
        [SerializeField] private RuntimeIntValue m_repeatCount = RuntimeIntValue.Fixed(2);
        [SerializeField] private RepeaterCompletionMode m_completionMode = RepeaterCompletionMode.RestartWhenReentered;

        public void SetRepeatCount(int repeatCount) => m_repeatCount = RuntimeIntValue.Fixed(Mathf.Max(1, repeatCount));
        public void SetRepeatCount(RuntimeIntValue repeatCount) => m_repeatCount = repeatCount;
        public void SetCompletionMode(RepeaterCompletionMode completionMode) => m_completionMode = completionMode;

        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatusReason(this, "No connected children");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            RepeaterState state = ctx.GetState<RepeaterState>(this);
            if (m_completionMode == RepeaterCompletionMode.RememberSuccess && state.completed) {
                ctx.SetStatusReason(this, "Already completed once");
                ctx.SetStatus(this, NodeStatus.Success);
                return NodeStatus.Success;
            }

            if (!state.initialized) {
                state.initialized = true;
                state.completedRepeats = 0;
                state.currentIndex = 0;
                state.repeatCount = Mathf.Max(1, m_repeatCount.Evaluate());
                ShootNode.ClearPlayerSnapshots(ctx);
            }

            while (state.completedRepeats < state.repeatCount) {
                while (state.currentIndex < children.Count) {
                    Node child = children[state.currentIndex];
                    if (child == null) {
                        int childNumber = state.currentIndex + 1;
                        ResetChildren(ctx);
                        ShootNode.ClearPlayerSnapshots(ctx);
                        ResetState(state, keepCompleted: false);
                        ctx.SetStatusReason(this, $"Child {childNumber} is missing");
                        ctx.SetStatus(this, NodeStatus.Failure);
                        return NodeStatus.Failure;
                    }

                    NodeStatus status = ctx.ExecuteNode(child);
                    switch (status) {
                        case NodeStatus.Running:
                            ctx.SetStatusReason(this, $"Loop {state.completedRepeats + 1} of {state.repeatCount}, child {state.currentIndex + 1} is still running");
                            ctx.SetStatus(this, NodeStatus.Running);
                            return NodeStatus.Running;

                        case NodeStatus.Failure:
                            ResetChildren(ctx);
                            ShootNode.ClearPlayerSnapshots(ctx);
                            ResetState(state, keepCompleted: false);
                            ctx.SetStatusReason(this, $"Stopped because child {state.currentIndex + 1} failed");
                            ctx.SetStatus(this, NodeStatus.Failure);
                            return NodeStatus.Failure;

                        case NodeStatus.Success:
                            ctx.ResetBranch(child);
                            state.currentIndex++;
                            break;
                    }
                }

                state.completedRepeats++;
                state.currentIndex = 0;
                ResetChildren(ctx);

                if (state.completedRepeats < state.repeatCount) {
                    ctx.SetStatusReason(this, $"Finished loop {state.completedRepeats} of {state.repeatCount}");
                    ctx.SetStatus(this, NodeStatus.Running);
                    return NodeStatus.Running;
                }
            }

            int completedLoops = state.completedRepeats;
            state.completed = true;
            ResetState(state, keepCompleted: m_completionMode == RepeaterCompletionMode.RememberSuccess);
            ShootNode.ClearPlayerSnapshots(ctx);
            ctx.SetStatusReason(this, $"Completed {completedLoops} loop(s)");
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }

        private void ResetChildren(NodeContext ctx) {
            foreach (Node child in children) {
                if (child != null)
                    ctx.ResetBranch(child);
            }
        }

        private static void ResetState(RepeaterState state, bool keepCompleted) {
            bool completed = keepCompleted && state.completed;
            state.initialized = false;
            state.completed = completed;
            state.completedRepeats = 0;
            state.currentIndex = 0;
            state.repeatCount = 0;
        }
    }
}
