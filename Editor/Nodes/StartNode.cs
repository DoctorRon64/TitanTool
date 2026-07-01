using System;
using TitanTool.Runtime.Nodes.Base;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.StartNode), "Start", "Flow/", BossGraphNodeCategory.Flow, tooltip: "Entry point for the boss graph.")]
    internal class StartNode : BossGraphNode {
        protected override bool hasInput => false;
        protected override int outputCount => 1;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.StartNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }
    }
}
