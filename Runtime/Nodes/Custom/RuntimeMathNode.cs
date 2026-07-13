using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public enum BlackboardNumberType {
        Int,
        Float
    }

    public enum BlackboardMathOperation {
        Set,
        Add,
        Subtract,
        Multiply,
        Divide
    }

    [NodeView("Runtime Math", "Action/Runtime/")]
    public class RuntimeMathNode : Node {
        [SerializeField] private string m_keyName = "Counter";
        [SerializeField] private BlackboardNumberType m_valueType = BlackboardNumberType.Int;
        [SerializeField] private BlackboardMathOperation m_operation = BlackboardMathOperation.Add;
        [SerializeField] private RuntimeIntValue m_intOperand = RuntimeIntValue.Fixed(1);
        [SerializeField] private RuntimeFloatValue m_floatOperand = RuntimeFloatValue.Fixed(1f);

        public void SetKeyName(string keyName) => m_keyName = keyName;
        public void SetValueType(BlackboardNumberType valueType) => m_valueType = valueType;
        public void SetOperation(BlackboardMathOperation operation) => m_operation = operation;
        public void SetIntOperand(RuntimeIntValue value) => m_intOperand = value;
        public void SetFloatOperand(RuntimeFloatValue value) => m_floatOperand = value;

        public override NodeStatus Tick(NodeContext ctx) {
            if (string.IsNullOrWhiteSpace(m_keyName)) {
                Debug.LogError($"{name}: Blackboard key is required.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            NodeStatus status = m_valueType == BlackboardNumberType.Float
                ? TickFloat(ctx)
                : TickInt(ctx);

            ctx.SetStatus(this, status);
            return status;
        }

        private NodeStatus TickInt(NodeContext ctx) {
            if (!TryGetCurrentInt(ctx, out int current))
                return NodeStatus.Failure;

            int operand = m_intOperand.Evaluate();
            if (m_operation == BlackboardMathOperation.Divide && operand == 0) {
                Debug.LogError($"{name}: Cannot divide blackboard key '{m_keyName}' by zero.");
                return NodeStatus.Failure;
            }

            int result = m_operation switch {
                BlackboardMathOperation.Set => operand,
                BlackboardMathOperation.Subtract => current - operand,
                BlackboardMathOperation.Multiply => current * operand,
                BlackboardMathOperation.Divide => current / operand,
                _ => current + operand
            };

            ctx.blackboard.SetValue(m_keyName, result);
            return NodeStatus.Success;
        }

        private NodeStatus TickFloat(NodeContext ctx) {
            if (!TryGetCurrentFloat(ctx, out float current))
                return NodeStatus.Failure;

            float operand = m_floatOperand.Evaluate();
            if (m_operation == BlackboardMathOperation.Divide && Mathf.Approximately(operand, 0f)) {
                Debug.LogError($"{name}: Cannot divide blackboard key '{m_keyName}' by zero.");
                return NodeStatus.Failure;
            }

            float result = m_operation switch {
                BlackboardMathOperation.Set => operand,
                BlackboardMathOperation.Subtract => current - operand,
                BlackboardMathOperation.Multiply => current * operand,
                BlackboardMathOperation.Divide => current / operand,
                _ => current + operand
            };

            ctx.blackboard.SetValue(m_keyName, result);
            return NodeStatus.Success;
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
