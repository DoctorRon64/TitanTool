using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.RepeaterNode), "Repeat Children", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Runs each connected child branch in order, then repeats that whole group for the configured loop count. A running child pauses the repeat, failure stops the repeat, and success advances to the next child or loop.")]
    public class RepeaterNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string OPTION_CHILD_COUNT = "ChildCount";
        private const string IN_PORT_REPEAT_COUNT = "RepeatCount";

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

            if (connectedChildren < 1) {
                context.Error("Repeater must have at least one connected child.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Repeater has {childCount} child slots but only {connectedChildren} connected.");
            }

            if (context.GetInputValue<int>(IN_PORT_REPEAT_COUNT) < 1)
                context.Error("Repeater repeat count must be at least 1.");
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(minimumChildCount, childCount);

            return 1;
        }
    }
}
