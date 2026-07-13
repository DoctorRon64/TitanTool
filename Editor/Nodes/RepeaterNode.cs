using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.RepeaterNode), "Repeat Child", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Runs exactly one child repeatedly for the configured loop count. Use Wait inside the child branch to space out repetitions.")]
    public class RepeaterNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_REPEAT_COUNT = "RepeatCount";

        protected override int outputCount => 1;
        protected override bool hasInput => true;
        protected override bool hasOutput => true;
        protected override string behaviorBadge => "xN";

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.RepeaterNode));
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

            if (connectedChildren != 1)
                context.Error("Repeater must have exactly one connected child.");

            if (context.GetInputValue<int>(IN_PORT_REPEAT_COUNT) < 1)
                context.Error("Repeater repeat count must be at least 1.");
        }
    }
}
