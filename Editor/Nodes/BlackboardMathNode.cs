using System;
using TitanTool.Runtime.Nodes.Custom;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.BlackboardMathNode), "Change Blackboard Number", "Action/Blackboard/", BossGraphNodeCategory.Action, tooltip: "Sets or modifies a numeric value stored on the blackboard.")]
    public class BlackboardMathNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_KEY_NAME = "KeyName";
        private const string IN_PORT_INT_OPERAND = "IntOperand";
        private const string IN_PORT_FLOAT_OPERAND = "FloatOperand";
        private const string OPTION_VALUE_TYPE = "ValueType";
        private const string OPTION_OPERATION = "Operation";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.BlackboardMathNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<BlackboardNumberType>(OPTION_VALUE_TYPE)
                .WithDisplayName("Number Type")
                .WithDefaultValue(BlackboardNumberType.Int)
                .Delayed();

            context.AddOption<BlackboardMathOperation>(OPTION_OPERATION)
                .WithDisplayName("Operation")
                .WithDefaultValue(BlackboardMathOperation.Add)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<string>(IN_PORT_KEY_NAME)
                .WithDisplayName("Blackboard Key")
                .WithDefaultValue("Counter")
                .Build();

            if (GetValueType() == BlackboardNumberType.Float) {
                context.AddInputPort<float>(IN_PORT_FLOAT_OPERAND)
                    .WithDisplayName("Operand")
                    .WithDefaultValue(1f)
                    .Build();
            } else {
                context.AddInputPort<int>(IN_PORT_INT_OPERAND)
                    .WithDisplayName("Operand")
                    .WithDefaultValue(1)
                    .Build();
            }
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.BlackboardMathNode mathRuntime)
                return;

            mathRuntime.SetKeyName(GraphNodePortUtility.GetInputValue<string>(this, IN_PORT_KEY_NAME));
            mathRuntime.SetValueType(GetValueType());
            mathRuntime.SetOperation(GetOperation());
            mathRuntime.SetIntOperand(GraphNodePortUtility.GetRuntimeIntValue(this, IN_PORT_INT_OPERAND));
            mathRuntime.SetFloatOperand(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_FLOAT_OPERAND));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (string.IsNullOrWhiteSpace(context.GetInputValue<string>(IN_PORT_KEY_NAME)))
                context.Error("Blackboard key is required.");

            if (GetOperation() == BlackboardMathOperation.Divide) {
                if (GetValueType() == BlackboardNumberType.Float) {
                    if (context.GetInputValue<float>(IN_PORT_FLOAT_OPERAND) == 0f)
                        context.Warning("Dividing by 0 will fail at runtime.");
                } else if (context.GetInputValue<int>(IN_PORT_INT_OPERAND) == 0) {
                    context.Warning("Dividing by 0 will fail at runtime.");
                }
            }
        }

        private BlackboardNumberType GetValueType() {
            if (GetNodeOptionByName(OPTION_VALUE_TYPE)?.TryGetValue(out BlackboardNumberType valueType) == true)
                return valueType;

            return BlackboardNumberType.Int;
        }

        private BlackboardMathOperation GetOperation() {
            if (GetNodeOptionByName(OPTION_OPERATION)?.TryGetValue(out BlackboardMathOperation operation) == true)
                return operation;

            return BlackboardMathOperation.Add;
        }
    }
}
