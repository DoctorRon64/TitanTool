using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.SequenceNode), "Run In Order", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Runs connected children from top to bottom. Stops on the first failure and succeeds only after every child succeeds.")]
    public class SequenceNode : BossGraphNode, IGraphNodeValidator {
        private const string OPTION_CHILD_COUNT = "ChildCount";
        protected override int outputCount => GetChildCount();
        protected override bool hasInput => true;
        protected override bool hasOutput => true;
        public override int minimumChildCount => 2;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.SequenceNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<int>(OPTION_CHILD_COUNT)
                .WithDisplayName("Children")
                .WithDefaultValue(2)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }

        public void Validate(BossGraphNodeValidationContext context) {
            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            int childCount = GetChildCount();

            if (connectedChildren < minimumChildCount) {
                context.Error("Sequence must have at least two connected children.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Sequence has {childCount} child slots but only {connectedChildren} connected.");
            }
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(minimumChildCount, childCount);

            return 2;
        }
    }
}
