using System;
using TitanTool.Runtime.Nodes.Custom;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.BlackboardCompareNode), "Check Blackboard Number", "Condition/Blackboard/", BossGraphNodeCategory.Condition, tooltip: "Passes when a blackboard number matches the selected comparison.")]
    public class BlackboardCompareNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_KEY_NAME = "KeyName";
        private const string IN_PORT_INT_THRESHOLD = "IntThreshold";
        private const string IN_PORT_FLOAT_THRESHOLD = "FloatThreshold";
        private const string OPTION_VALUE_TYPE = "ValueType";
        private const string OPTION_COMPARISON = "Comparison";

        protected override bool hasInput => true;
        protected override bool hasOutput => false;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.BlackboardCompareNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<BlackboardNumberType>(OPTION_VALUE_TYPE)
                .WithDisplayName("Number Type")
                .WithDefaultValue(BlackboardNumberType.Int)
                .Delayed();

            context.AddOption<BlackboardComparison>(OPTION_COMPARISON)
                .WithDisplayName("Comparison")
                .WithDefaultValue(BlackboardComparison.GreaterOrEqual)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<string>(IN_PORT_KEY_NAME)
                .WithDisplayName("Blackboard Key")
                .WithDefaultValue("Counter")
                .Build();

            if (GetValueType() == BlackboardNumberType.Float) {
                context.AddInputPort<float>(IN_PORT_FLOAT_THRESHOLD)
                    .WithDisplayName("Compare To")
                    .WithDefaultValue(3f)
                    .Build();
            } else {
                context.AddInputPort<int>(IN_PORT_INT_THRESHOLD)
                    .WithDisplayName("Compare To")
                    .WithDefaultValue(3)
                    .Build();
            }
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.BlackboardCompareNode compareRuntime)
                return;

            compareRuntime.SetKeyName(GraphNodePortUtility.GetInputValue<string>(this, IN_PORT_KEY_NAME));
            compareRuntime.SetValueType(GetValueType());
            compareRuntime.SetComparison(GetComparison());
            compareRuntime.SetIntThreshold(GraphNodePortUtility.GetRuntimeIntValue(this, IN_PORT_INT_THRESHOLD));
            compareRuntime.SetFloatThreshold(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_FLOAT_THRESHOLD));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (string.IsNullOrWhiteSpace(context.GetInputValue<string>(IN_PORT_KEY_NAME)))
                context.Error("Blackboard key is required.");
        }

        private BlackboardNumberType GetValueType() {
            if (GetNodeOptionByName(OPTION_VALUE_TYPE)?.TryGetValue(out BlackboardNumberType valueType) == true)
                return valueType;

            return BlackboardNumberType.Int;
        }

        private BlackboardComparison GetComparison() {
            if (GetNodeOptionByName(OPTION_COMPARISON)?.TryGetValue(out BlackboardComparison comparison) == true)
                return comparison;

            return BlackboardComparison.GreaterOrEqual;
        }
    }
}
