using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.RunOnceNode), "Run Once", "Decorator/", BossGraphNodeCategory.Decorator, tooltip: "Runs its child one time. After that, later visits immediately return the configured status.")]
    public class RunOnceNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string OPTION_COMPLETED_STATUS = "CompletedStatus";

        protected override int outputCount => 1;
        protected override bool hasInput => true;
        protected override bool hasOutput => true;
        protected override string behaviorBadge => "ONCE";

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.RunOnceNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<RunOnceCompletedStatus>(OPTION_COMPLETED_STATUS)
                .WithDisplayName("Then Return")
                .WithDefaultValue(RunOnceCompletedStatus.Failure)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Base.RunOnceNode runOnceRuntime)
                return;

            runOnceRuntime.SetCompletedStatus(GetCompletedStatus());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            if (connectedChildren == 0)
                context.Error("Run Once must have one connected child.");
            else if (connectedChildren > 1)
                context.Warning("Run Once uses only the first connected child.");
        }

        private RunOnceCompletedStatus GetCompletedStatus() {
            if (GetNodeOptionByName(OPTION_COMPLETED_STATUS)?.TryGetValue(out RunOnceCompletedStatus completedStatus) == true)
                return completedStatus;

            return RunOnceCompletedStatus.Failure;
        }
    }
}
