using System;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.SwapSpriteNode), "Set Sprite", "Action/", BossGraphNodeCategory.Action, tooltip: "Changes the boss SpriteRenderer to the selected sprite.")]
    public class SwapSpriteNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_SPRITE = "Sprite";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.SwapSpriteNode));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<Sprite>(IN_PORT_SPRITE)
                .WithDisplayName("New Sprite")
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.SwapSpriteNode swapRuntime)
                return;

            swapRuntime.SetSprite(GraphNodePortUtility.GetInputValue<Sprite>(this, IN_PORT_SPRITE));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<Sprite>(IN_PORT_SPRITE) == null)
                context.Error("Swap Sprite requires a sprite.");
        }
    }
}
