using System;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.WaitNode), "Wait", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Wait, "Pauses this branch for the configured duration.")]
    internal class WaitNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        public const string IN_PORT_DURATION = "InDuration";
        
        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.WaitNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<float>(IN_PORT_DURATION)
                .WithDisplayName("Duration Sec")
                .WithDefaultValue(1f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is TitanTool.Runtime.Nodes.Custom.WaitNode waitRuntime) {
                waitRuntime.SetDuration(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_DURATION));
            }
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<float>(IN_PORT_DURATION) <= 0f) {
                context.Error("Wait duration must be greater than 0.");
            }
        }
    }
}
