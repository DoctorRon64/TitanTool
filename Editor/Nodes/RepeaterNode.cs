using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.RepeaterNode), "Repeat Child", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Runs its child repeatedly for the configured number of loops.")]
    public class RepeaterNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_REPEAT_COUNT = "RepeatCount";
        private const string OPTION_CHILD_COUNT = "ChildCount";

        protected override int outputCount => GetChildCount();
        protected override bool hasInput => true;
        protected override bool hasOutput => true;
        protected override string behaviorBadge => "xN";

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.RepeaterNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<int>(OPTION_CHILD_COUNT)
                .WithDisplayName("Children")
                .WithDefaultValue(1)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<int>(IN_PORT_REPEAT_COUNT)
                .WithDisplayName("Loops")
                .WithDefaultValue(2)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Base.RepeaterNode repeaterRuntime)
                return;

            repeaterRuntime.SetRepeatCount(GraphNodePortUtility.GetRuntimeIntValue(this, IN_PORT_REPEAT_COUNT));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            int childCount = GetChildCount();

            if (connectedChildren == 0) {
                context.Error("Repeater must have at least one connected child.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Repeater has {childCount} child slots but only {connectedChildren} connected.");
            }

            if (context.GetInputValue<int>(IN_PORT_REPEAT_COUNT) < 1)
                context.Error("Repeater repeat count must be at least 1.");
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(1, childCount);

            return 1;
        }
    }
}
