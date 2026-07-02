using System;
using System.Collections.Generic;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Base.WeightedRandomSelectorNode), "Pick Weighted Child", "Composite/", BossGraphNodeCategory.Composite, tooltip: "Chooses one child at random, using weights to favor some branches.")]
    public class WeightedRandomSelectorNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string OPTION_CHILD_COUNT = "ChildCount";
        private const string IN_PORT_WEIGHT_PREFIX = "Weight";

        protected override int outputCount => GetChildCount();
        protected override bool hasInput => true;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.WeightedRandomSelectorNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<int>(OPTION_CHILD_COUNT)
                .WithDisplayName("Children")
                .WithDefaultValue(2)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            IReadOnlyList<string> outputPortNames = AddInputOutputExecutionPorts(context);

            for (int i = 0; i < outputPortNames.Count; i++) {
                int portIndex = GetExecutionOutputPortIndex(outputPortNames[i]);
                context.AddInputPort<float>(GetWeightPortName(portIndex))
                    .WithDisplayName($"Weight {i}")
                    .WithDefaultValue(1f)
                    .Build();
            }
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Base.WeightedRandomSelectorNode weightedRuntime)
                return;

            List<RuntimeFloatValue> weights = new();
            foreach (string outputPortName in GetExecutionOutputPortNames()) {
                int portIndex = GetExecutionOutputPortIndex(outputPortName);
                weights.Add(GraphNodePortUtility.GetRuntimeFloatValue(this, GetWeightPortName(portIndex)));
            }

            weightedRuntime.SetWeights(weights);
        }

        public void Validate(BossGraphNodeValidationContext context) {
            int connectedChildren = BossGraphValidator.GetConnectedChildren(this).Count();
            int childCount = GetChildCount();

            if (connectedChildren == 0) {
                context.Error("Weighted Random Selector must have at least one connected child.");
            } else if (connectedChildren < childCount) {
                context.Warning($"Weighted Random Selector has {childCount} child slots but only {connectedChildren} connected.");
            }

            bool anyPositiveWeight = false;
            IReadOnlyList<string> outputPortNames = GetExecutionOutputPortNames();
            for (int i = 0; i < outputPortNames.Count; i++) {
                int portIndex = GetExecutionOutputPortIndex(outputPortNames[i]);
                float weight = context.GetInputValue<float>(GetWeightPortName(portIndex));
                if (weight < 0f)
                    context.Error($"Weight {i} cannot be negative.");
                if (weight > 0f)
                    anyPositiveWeight = true;
            }

            if (!anyPositiveWeight)
                context.Warning("All weights are zero. The first connected child will be used.");
        }

        private int GetChildCount() {
            if (GetNodeOptionByName(OPTION_CHILD_COUNT)?.TryGetValue(out int childCount) == true)
                return Math.Max(1, childCount);

            return 2;
        }

        private static string GetWeightPortName(int index) => $"{IN_PORT_WEIGHT_PREFIX}{index}";
    }
}
