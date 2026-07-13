using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.RerouteNode), "Reroute", "Utility/", BossGraphNodeCategory.Utility, BossGraphNodeIcons.Reroute, "Redirects flow wires to keep the graph layout tidy.")]
    public class RerouteNode : BossGraphNode, IGraphNodeValidator {
        protected override int outputCount => 1;
        protected override bool hasInput => true;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.RerouteNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (!BossGraphValidator.GetConnectedChildren(this).Any())
                context.Warning("Reroute node is not connected to a child node.");
        }
    }
}
