using TitanTool.Runtime.Values;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class RepeaterState {
        public bool initialized;
        public int currentIndex;
        public int completedRepeats;
        public int repeatCount;
    }

    [NodeView("Repeat Child", "Composite/")]
    public class RepeaterNode : Node {
        [SerializeField] private RuntimeIntValue m_repeatCount = RuntimeIntValue.Fixed(2);

        public void SetRepeatCount(int repeatCount) => m_repeatCount = RuntimeIntValue.Fixed(Mathf.Max(1, repeatCount));
        public void SetRepeatCount(RuntimeIntValue repeatCount) => m_repeatCount = repeatCount;

        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            RepeaterState state = ctx.GetState<RepeaterState>(this);
            if (!state.initialized) {
                state.initialized = true;
                state.currentIndex = 0;
                state.completedRepeats = 0;
                state.repeatCount = Mathf.Max(1, m_repeatCount.Evaluate());
                ShootPatternNode.ClearPlayerSnapshots(ctx);
            }

            while (state.completedRepeats < state.repeatCount) {
                while (state.currentIndex < children.Count) {
                    Node child = children[state.currentIndex];
                    if (child == null) {
                        ResetChildren(ctx);
                        ResetState(state);
                        ctx.SetStatus(this, NodeStatus.Failure);
                        return NodeStatus.Failure;
                    }

                    NodeStatus status = ctx.ExecuteNode(child);
                    switch (status) {
                        case NodeStatus.Running:
                            ctx.SetStatus(this, NodeStatus.Running);
                            return NodeStatus.Running;

                        case NodeStatus.Failure:
                            ResetChildren(ctx);
                            ShootPatternNode.ClearPlayerSnapshots(ctx);
                            ResetState(state);
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
                    ctx.SetStatus(this, NodeStatus.Running);
                    return NodeStatus.Running;
                }
            }

            ResetState(state);
            ShootPatternNode.ClearPlayerSnapshots(ctx);
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }

        private void ResetChildren(NodeContext ctx) {
            foreach (Node child in children)
                ctx.ResetBranch(child);
        }

        private static void ResetState(RepeaterState state) {
            state.initialized = false;
            state.currentIndex = 0;
            state.completedRepeats = 0;
            state.repeatCount = 0;
        }
    }
}
