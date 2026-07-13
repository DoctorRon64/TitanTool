using System;
using System.Linq;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.ShuffleBagNode), "Shuffle Bag", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Picks from a random bag of connected children, then refills the bag after each child has been chosen.")]
    public class ShuffleBagNode : BossGraphNode, IGraphNodeValidator {
        private const string OPTION_CHILD_COUNT = "ChildCount";

        protected override int outputCount => GetChildCount();
        protected override bool hasInput => true;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.ShuffleBagNode));
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

            if (connectedChildren == 0) {
                context.Error("Shuffle Bag must have at least one connected child.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Shuffle Bag has {childCount} child slots but only {connectedChildren} connected.");
            }
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(1, childCount);

            return 2;
        }
    }
}
