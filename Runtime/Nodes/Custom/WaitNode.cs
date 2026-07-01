using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public class WaitState {
        public float elapsed;
        public float duration;
        public bool started;
    }

    [NodeView("Wait", "Action/")]
    public class WaitNode : ActionNode {
        [SerializeField] private RuntimeFloatValue m_duration = RuntimeFloatValue.Fixed(1f);

        public float duration {
            get => m_duration.Evaluate();
            set => m_duration = RuntimeFloatValue.Fixed(Mathf.Max(0f, value));
        }

        public void SetDuration(RuntimeFloatValue durationValue) => m_duration = durationValue;

        public override NodeStatus Tick(NodeContext ctx) {
            WaitState state = ctx.GetState<WaitState>(this);

            if (!state.started) {
                state.started = true;
                state.elapsed = 0f;
                state.duration = Mathf.Max(0f, m_duration.Evaluate());
                if (ctx.debugLogging) {
                    Debug.Log($"{name}: waiting for {state.duration:F2}s.");
                }
            }

            state.elapsed += ctx.deltaTime;

            if (state.elapsed < state.duration) {
                ctx.SetStatus(this, NodeStatus.Running);
                return NodeStatus.Running;
            }
            
            ctx.ResetNode(this);
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }
    }
}
