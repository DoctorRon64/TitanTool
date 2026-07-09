using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class CooldownState {
        public bool initialized;
        public bool childCompleted;
        public float remaining;
        public float duration;
    }

    [NodeView("Cooldown Gate", "Decorator/")]
    public class CooldownNode : ActionNode {
        [SerializeField] private RuntimeFloatValue m_duration = RuntimeFloatValue.Fixed(1f);
        [SerializeField] private bool m_startReady = true;

        public void SetDuration(float duration) => m_duration = RuntimeFloatValue.Fixed(Mathf.Max(0f, duration));
        public void SetDuration(RuntimeFloatValue duration) => m_duration = duration;
        public void SetStartReady(bool startReady) => m_startReady = startReady;

        public override NodeStatus Tick(NodeContext ctx) {
            if (child == null) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            CooldownState state = ctx.GetState<CooldownState>(this);
            if (!state.initialized) {
                state.initialized = true;
                state.duration = Mathf.Max(0f, m_duration.Evaluate());
                state.remaining = m_startReady ? 0f : state.duration;
            }

            if (state.remaining > 0f) {
                state.remaining = Mathf.Max(0f, state.remaining - ctx.deltaTime);
                if (state.remaining > 0f) {
                    ctx.SetStatus(this, NodeStatus.Running);
                    return NodeStatus.Running;
                }

                if (state.childCompleted) {
                    ctx.ResetNode(this);
                    ctx.SetStatus(this, NodeStatus.Success);
                    return NodeStatus.Success;
                }
            }

            NodeStatus result = ctx.ExecuteNode(child);
            if (result == NodeStatus.Success) {
                state.duration = Mathf.Max(0f, m_duration.Evaluate());
                state.remaining = state.duration;
                state.childCompleted = true;
                ctx.ResetBranch(child);

                if (state.remaining > 0f) {
                    ctx.SetStatus(this, NodeStatus.Running);
                    return NodeStatus.Running;
                }

                ctx.ResetNode(this);
            }

            ctx.SetStatus(this, result);
            return result;
        }
    }
}
