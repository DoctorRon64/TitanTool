using TitanTool.Runtime.Values;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class RepeaterState {
        public bool initialized;
        public int completedRepeats;
        public int repeatCount;
    }

    [NodeView("Repeat Child", "Composite/")]
    public class RepeaterNode : Node {
        [SerializeField] private RuntimeIntValue m_repeatCount = RuntimeIntValue.Fixed(2);

        public void SetRepeatCount(int repeatCount) => m_repeatCount = RuntimeIntValue.Fixed(Mathf.Max(1, repeatCount));
        public void SetRepeatCount(RuntimeIntValue repeatCount) => m_repeatCount = repeatCount;

        public override NodeStatus Tick(NodeContext ctx) {
            Node repeatedChild = children.Count > 0 ? children[0] : null;
            if (repeatedChild == null) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            RepeaterState state = ctx.GetState<RepeaterState>(this);
            if (!state.initialized) {
                state.initialized = true;
                state.completedRepeats = 0;
                state.repeatCount = Mathf.Max(1, m_repeatCount.Evaluate());
                ShootNode.ClearPlayerSnapshots(ctx);
            }

            while (state.completedRepeats < state.repeatCount) {
                NodeStatus status = ctx.ExecuteNode(repeatedChild);
                switch (status) {
                    case NodeStatus.Running:
                        ctx.SetStatus(this, NodeStatus.Running);
                        return NodeStatus.Running;

                    case NodeStatus.Failure:
                        ResetChild(ctx, repeatedChild);
                        ShootNode.ClearPlayerSnapshots(ctx);
                        ResetState(state);
                        ctx.SetStatus(this, NodeStatus.Failure);
                        return NodeStatus.Failure;

                    case NodeStatus.Success:
                        ctx.ResetBranch(repeatedChild);
                        break;
                }

                state.completedRepeats++;
                ResetChild(ctx, repeatedChild);

                if (state.completedRepeats < state.repeatCount) {
                    ctx.SetStatus(this, NodeStatus.Running);
                    return NodeStatus.Running;
                }
            }

            ResetState(state);
            ShootNode.ClearPlayerSnapshots(ctx);
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }

        private static void ResetChild(NodeContext ctx, Node child) {
            if (child != null)
                ctx.ResetBranch(child);
        }

        private static void ResetState(RepeaterState state) {
            state.initialized = false;
            state.completedRepeats = 0;
            state.repeatCount = 0;
        }
    }
}
