using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public enum BlackboardComparison {
        Less,
        LessOrEqual,
        Equal,
        GreaterOrEqual,
        Greater,
        NotEqual
    }

    [NodeView("Check Blackboard Number", "Condition/Blackboard/")]
    public class BlackboardCompareNode : Node {
        [SerializeField] private string m_keyName = "Counter";
        [SerializeField] private BlackboardNumberType m_valueType = BlackboardNumberType.Int;
        [SerializeField] private BlackboardComparison m_comparison = BlackboardComparison.GreaterOrEqual;
        [SerializeField] private RuntimeIntValue m_intThreshold = RuntimeIntValue.Fixed(3);
        [SerializeField] private RuntimeFloatValue m_floatThreshold = RuntimeFloatValue.Fixed(3f);

        public void SetKeyName(string keyName) => m_keyName = keyName;
        public void SetValueType(BlackboardNumberType valueType) => m_valueType = valueType;
        public void SetComparison(BlackboardComparison comparison) => m_comparison = comparison;
        public void SetIntThreshold(RuntimeIntValue value) => m_intThreshold = value;
        public void SetFloatThreshold(RuntimeFloatValue value) => m_floatThreshold = value;

        public override NodeStatus Tick(NodeContext ctx) {
            if (string.IsNullOrWhiteSpace(m_keyName)) {
                Debug.LogError($"{name}: Blackboard key is required.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            bool passed = m_valueType == BlackboardNumberType.Float
                ? CompareFloat(ctx)
                : CompareInt(ctx);

            NodeStatus status = passed ? NodeStatus.Success : NodeStatus.Failure;
            ctx.SetStatus(this, status);
            return status;
        }

        private bool CompareInt(NodeContext ctx) {
            if (!TryGetCurrentInt(ctx, out int current))
                return false;

            int threshold = m_intThreshold.Evaluate();
            return m_comparison switch {
                BlackboardComparison.Less => current < threshold,
                BlackboardComparison.LessOrEqual => current <= threshold,
                BlackboardComparison.Equal => current == threshold,
                BlackboardComparison.Greater => current > threshold,
                BlackboardComparison.NotEqual => current != threshold,
                _ => current >= threshold
            };
        }

        private bool CompareFloat(NodeContext ctx) {
            if (!TryGetCurrentFloat(ctx, out float current))
                return false;

            float threshold = m_floatThreshold.Evaluate();
            return m_comparison switch {
                BlackboardComparison.Less => current < threshold,
                BlackboardComparison.LessOrEqual => current <= threshold,
                BlackboardComparison.Equal => Mathf.Approximately(current, threshold),
                BlackboardComparison.Greater => current > threshold,
                BlackboardComparison.NotEqual => !Mathf.Approximately(current, threshold),
                _ => current >= threshold
            };
        }

        private bool TryGetCurrentInt(NodeContext ctx, out int current) {
            current = 0;
            if (!ctx.blackboard.values.TryGetValue(m_keyName, out object rawValue))
                return true;

            if (rawValue is int intValue) {
                current = intValue;
                return true;
            }

            Debug.LogError($"{name}: Blackboard key '{m_keyName}' is not an Int.");
            return false;
        }

        private bool TryGetCurrentFloat(NodeContext ctx, out float current) {
            current = 0f;
            if (!ctx.blackboard.values.TryGetValue(m_keyName, out object rawValue))
                return true;

            if (rawValue is float floatValue) {
                current = floatValue;
                return true;
            }

            Debug.LogError($"{name}: Blackboard key '{m_keyName}' is not a Float.");
            return false;
        }
    }
}
