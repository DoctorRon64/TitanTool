using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Custom;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.PhaseNode), "Health Phase", "Decorator/", BossGraphNodeCategory.Decorator, tooltip: "Runs its child only while boss health is inside this percent range.")]
    public class PhaseNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_MIN_HEALTH_PERCENT = "MinHealthPercent";
        private const string IN_PORT_MAX_HEALTH_PERCENT = "MaxHealthPercent";

        protected override int outputCount => 1;
        protected override bool hasInput => true;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.PhaseNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<float>(IN_PORT_MIN_HEALTH_PERCENT)
                .WithDisplayName("Min Health %")
                .WithDefaultValue(0f)
                .Build();

            context.AddInputPort<float>(IN_PORT_MAX_HEALTH_PERCENT)
                .WithDisplayName("Max Health %")
                .WithDefaultValue(100f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.PhaseNode phaseRuntime)
                return;

            phaseRuntime.SetMinHealthPercent(GraphNodePortUtility.GetInputValue<float>(this, IN_PORT_MIN_HEALTH_PERCENT));
            phaseRuntime.SetMaxHealthPercent(GraphNodePortUtility.GetInputValue<float>(this, IN_PORT_MAX_HEALTH_PERCENT));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            float minHealth = context.GetInputValue<float>(IN_PORT_MIN_HEALTH_PERCENT);
            float maxHealth = context.GetInputValue<float>(IN_PORT_MAX_HEALTH_PERCENT);

            if (minHealth < 0f || minHealth > 100f)
                context.Error("Phase lower health percentage must be between 0 and 100.");

            if (maxHealth < 0f || maxHealth > 100f)
                context.Error("Phase upper health percentage must be between 0 and 100.");

            if (minHealth >= maxHealth)
                context.Error("Phase lower health percentage must be less than its upper percentage.");

            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            if (connectedChildren > 1)
                context.Warning("Phase uses only the first connected child.");
        }
    }
}
