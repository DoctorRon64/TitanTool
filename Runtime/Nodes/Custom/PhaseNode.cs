using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    [NodeView("Health Phase", "Decorator/")]
    public class PhaseNode : ActionNode {
        [SerializeField, Range(0f, 100f)] private float m_minHealthPercent;
        [SerializeField, Range(0f, 100f)] private float m_maxHealthPercent = 100f;

        public void SetMinHealthPercent(float value) => m_minHealthPercent = Mathf.Clamp(value, 0f, 100f);
        public void SetMaxHealthPercent(float value) => m_maxHealthPercent = Mathf.Clamp(value, 0f, 100f);

        public override NodeStatus Tick(NodeContext ctx) {
            if (!ctx.blackboard.TryGet(BKeys.BossHp, out int currentHealth) ||
                !ctx.blackboard.TryGet(BKeys.BossMaxHp, out int maxHealth) ||
                maxHealth <= 0) {
                Debug.LogWarning($"{name}: Boss current and maximum HP are required for phase evaluation.");
                if (child != null)
                    ctx.ResetBranch(child);
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            float healthPercent = currentHealth / (float)maxHealth * 100f;
            bool isActive = healthPercent > m_minHealthPercent &&
                            healthPercent <= m_maxHealthPercent;

            if (!isActive) {
                if (child != null)
                    ctx.ResetBranch(child);
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            if (child == null) {
                ctx.SetStatus(this, NodeStatus.Success);
                return NodeStatus.Success;
            }

            NodeStatus result = ctx.ExecuteNode(child);
            ctx.SetStatus(this, result);
            return result;
        }
    }
}
