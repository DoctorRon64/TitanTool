using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.CooldownNode), "Cooldown Gate", "Decorator/", BossGraphNodeCategory.Decorator, tooltip: "Runs its child, then keeps the branch running until the cooldown finishes.")]
    public class CooldownNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_DURATION = "Duration";
        private const string OPTION_START_READY = "StartReady";

        protected override int outputCount => 1;
        protected override bool hasInput => true;
        protected override bool hasOutput => true;
        protected override string behaviorBadge => "CD";

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.CooldownNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<bool>(OPTION_START_READY)
                .WithDisplayName("Ready First")
                .WithDefaultValue(true)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<float>(IN_PORT_DURATION)
                .WithDisplayName("Cooldown")
                .WithDefaultValue(1f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Base.CooldownNode cooldownRuntime)
                return;

            cooldownRuntime.SetDuration(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_DURATION));
            cooldownRuntime.SetStartReady(GetStartReady());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<float>(IN_PORT_DURATION) < 0f)
                context.Error("Cooldown duration cannot be negative.");

            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            if (connectedChildren == 0)
                context.Error("Cooldown must have one connected child.");
            else if (connectedChildren > 1)
                context.Warning("Cooldown uses only the first connected child.");
        }

        private bool GetStartReady() {
            if (GetNodeOptionByName(OPTION_START_READY)?.TryGetValue(out bool startReady) == true)
                return startReady;

            return true;
        }
    }
}
