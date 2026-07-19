using System;
using TitanTool.Runtime.Nodes.Custom;
using Unity.GraphToolkit.Editor;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(
        typeof(TitanTool.Runtime.Nodes.Custom.ScriptableActionNode),
        "Custom Scriptable Node",
        "Action/Custom/",
        BossGraphNodeCategory.Action,
        BossGraphNodeIcons.Action,
        "Runs a ScriptableObject-based custom action so project scripts can trigger polish, package integrations, or boss-specific behavior.",
        "custom scriptable scriptableobject user extension dotween feel unitask polish tween feedback async")]
    public class ScriptableActionNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_NODE_ASSET = "NodeAsset";

        protected override bool hasInput => true;
        protected override bool hasOutput => false;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.ScriptableActionNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<TitanToolScriptableNode>(IN_PORT_NODE_ASSET)
                .WithDisplayName("Node Asset")
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.ScriptableActionNode scriptableRuntime)
                return;

            scriptableRuntime.SetNodeAsset(GraphNodePortUtility.GetInputValue<TitanToolScriptableNode>(this, IN_PORT_NODE_ASSET));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<TitanToolScriptableNode>(IN_PORT_NODE_ASSET) == null)
                context.Error("Custom Scriptable Node requires a node asset.");
        }
    }
}
