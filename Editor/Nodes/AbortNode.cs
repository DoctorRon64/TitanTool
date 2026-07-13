using System;
using TitanTool.Runtime.Nodes.Custom;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.AbortNode), "Abort", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Action, "Stops currently running child actions and clears their stored runtime state. Use it to interrupt movement, waits, or long-running branches.")]
    public class AbortNode : BossGraphNode, IRuntimeNodeCompiler {
        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.AbortNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }

        public void Compile(RuntimeNode runtimeNode) {
        }
    }
}
