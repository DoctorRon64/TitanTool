using System;
using TitanTool.Runtime.Nodes.Custom;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.AbortNode), "Abort", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Action, "Cancels currently running actions and clears their runtime state.")]
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
