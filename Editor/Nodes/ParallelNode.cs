using System;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.ParallelNode), "Run Together", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Ticks unfinished child branches in the same graph update. Running children keep ticking, completed children wait, and the selected preset decides when the whole node succeeds or fails.")]
    public class ParallelNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string OPTION_CHILD_COUNT = "ChildCount";
        private const string OPTION_PRESET = "Preset";
        private const string LEGACY_OPTION_SUCCESS_RULE = "SuccessRule";
        private const string LEGACY_OPTION_FAILURE_RULE = "FailureRule";

        protected override int outputCount => GetChildCount();
        protected override bool hasInput => true;
        protected override bool hasOutput => true;
        protected override string behaviorBadge => "PAR";
        public override int minimumChildCount => 2;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.ParallelNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<int>(OPTION_CHILD_COUNT)
                .WithDisplayName("Children")
                .WithDefaultValue(2)
                .Delayed();

            context.AddOption<ParallelBehaviorPreset>(OPTION_PRESET)
                .WithDisplayName("Finish Rule")
                .WithDefaultValue(ParallelBehaviorPreset.FailFast)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Base.ParallelNode parallelRuntime)
                return;

            parallelRuntime.SetPreset(GetPreset());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            int childCount = GetChildCount();

            if (connectedChildren < minimumChildCount) {
                context.Error("Parallel must have at least two connected children.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Parallel has {childCount} child slots but only {connectedChildren} connected.");
            }

            if (GetPreset() == ParallelBehaviorPreset.FirstResultWins) {
                context.Warning("First Result Wins ends the parallel group on the first child result. If success and failure happen during the same update, failure takes priority.");
            }
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(minimumChildCount, childCount);

            return 2;
        }

        private ParallelBehaviorPreset GetPreset() {
            if (GetNodeOptionByName(OPTION_PRESET)?.TryGetValue(out ParallelBehaviorPreset preset) == true)
                return preset;

            if (TryGetLegacyRules(out ParallelSuccessRule successRule, out ParallelFailureRule failureRule)) {
                if (successRule == ParallelSuccessRule.AnyChild && failureRule == ParallelFailureRule.AllChildren)
                    return ParallelBehaviorPreset.AnySuccessWins;

                if (successRule == ParallelSuccessRule.AllChildren && failureRule == ParallelFailureRule.AllChildren)
                    return ParallelBehaviorPreset.WaitForAll;

                if (successRule == ParallelSuccessRule.AnyChild && failureRule == ParallelFailureRule.AnyChild)
                    return ParallelBehaviorPreset.FirstResultWins;
            }

            return ParallelBehaviorPreset.FailFast;
        }

        private bool TryGetLegacyRules(out ParallelSuccessRule successRule, out ParallelFailureRule failureRule) {
            successRule = ParallelSuccessRule.AllChildren;
            failureRule = ParallelFailureRule.AnyChild;
            bool hasSuccessRule = GetNodeOptionByName(LEGACY_OPTION_SUCCESS_RULE)?.TryGetValue(out successRule) == true;
            bool hasFailureRule = GetNodeOptionByName(LEGACY_OPTION_FAILURE_RULE)?.TryGetValue(out failureRule) == true;
            return hasSuccessRule || hasFailureRule;
        }
    }
}
