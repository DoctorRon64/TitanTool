using System;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.DelayNode), "Delay", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Wait, "Keeps this branch Running for the configured duration, then returns Success.")]
    internal class DelayNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        public const string IN_PORT_DURATION = "InDuration";
        
        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.DelayNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<float>(IN_PORT_DURATION)
                .WithDisplayName("Duration Sec")
                .WithDefaultValue(1f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is TitanTool.Runtime.Nodes.Custom.DelayNode delayRuntime) {
                delayRuntime.SetDuration(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_DURATION));
            }
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<float>(IN_PORT_DURATION) <= 0f) {
                context.Error("Delay duration must be greater than 0.");
            }
        }
    }
}
