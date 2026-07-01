using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.ParallelNode), "Run In Parallel", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Runs all child branches at the same time and returns based on the success and failure rules.")]
    public class ParallelNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string OPTION_CHILD_COUNT = "ChildCount";
        private const string OPTION_SUCCESS_RULE = "SuccessRule";
        private const string OPTION_FAILURE_RULE = "FailureRule";

        protected override int outputCount => GetChildCount();
        protected override bool hasInput => true;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.ParallelNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<int>(OPTION_CHILD_COUNT)
                .WithDisplayName("Children")
                .WithDefaultValue(2)
                .Delayed();

            context.AddOption<ParallelSuccessRule>(OPTION_SUCCESS_RULE)
                .WithDisplayName("Succeed When")
                .WithDefaultValue(ParallelSuccessRule.AllChildren)
                .Delayed();

            context.AddOption<ParallelFailureRule>(OPTION_FAILURE_RULE)
                .WithDisplayName("Fail When")
                .WithDefaultValue(ParallelFailureRule.AnyChild)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Base.ParallelNode parallelRuntime)
                return;

            parallelRuntime.SetSuccessRule(GetSuccessRule());
            parallelRuntime.SetFailureRule(GetFailureRule());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            int childCount = GetChildCount();

            if (connectedChildren == 0) {
                context.Error("Parallel must have at least one connected child.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Parallel has {childCount} child slots but only {connectedChildren} connected.");
            }

            if (connectedChildren == 1)
                context.Warning("Parallel has only one connected child, so it behaves like a direct connection.");

            if (GetSuccessRule() == ParallelSuccessRule.AnyChild &&
                GetFailureRule() == ParallelFailureRule.AnyChild) {
                context.Warning("Parallel is configured to finish on any success or failure. If both happen during the same update, failure takes priority.");
            }
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(1, childCount);

            return 2;
        }

        private ParallelSuccessRule GetSuccessRule() {
            if (GetNodeOptionByName(OPTION_SUCCESS_RULE)?.TryGetValue(out ParallelSuccessRule rule) == true)
                return rule;

            return ParallelSuccessRule.AllChildren;
        }

        private ParallelFailureRule GetFailureRule() {
            if (GetNodeOptionByName(OPTION_FAILURE_RULE)?.TryGetValue(out ParallelFailureRule rule) == true)
                return rule;

            return ParallelFailureRule.AnyChild;
        }
    }
}
