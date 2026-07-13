using System;
using TitanTool.Runtime;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.TeleportToNode), "Teleport To Target", "Action/Movement/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Movement, "Instantly moves the boss to a player, point, or fixed position.")]
    public class TeleportToNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_TARGET_POSITION = "TargetPosition";
        private const string IN_PORT_OFFSET = "Offset";
        private const string IN_PORT_TARGET_POINT = "TargetPoint";
        private const string OPTION_TARGET_SOURCE = "TargetSource";
        private const string OPTION_STOP_MOVEMENT = "StopMovement";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.TeleportToNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<SpawnPositionSource>(OPTION_TARGET_SOURCE)
                .WithDisplayName("Target Source")
                .WithDefaultValue(SpawnPositionSource.TargetPoint)
                .Delayed();

            context.AddOption<bool>(OPTION_STOP_MOVEMENT)
                .WithDisplayName("Stop Movement")
                .WithDefaultValue(true)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            switch (GetTargetSource()) {
                case SpawnPositionSource.FixedPosition:
                    context.AddInputPort<Vector2>(IN_PORT_TARGET_POSITION)
                        .WithDisplayName("World Position")
                        .WithDefaultValue(Vector2.zero)
                        .Build();
                    break;

                case SpawnPositionSource.TargetPoint:
                    context.AddInputPort<TargetPointKey>(IN_PORT_TARGET_POINT)
                        .WithDisplayName("Target Point Key")
                        .Build();
                    break;
            }

            context.AddInputPort<Vector2>(IN_PORT_OFFSET)
                .WithDisplayName("Target Offset")
                .WithDefaultValue(Vector2.zero)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.TeleportToNode teleportRuntime)
                return;

            teleportRuntime.SetTargetSource(GetTargetSource());
            teleportRuntime.SetTargetPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_TARGET_POSITION));
            teleportRuntime.SetOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_OFFSET));
            teleportRuntime.SetSpawnPointKey(GraphNodePortUtility.GetInputValue<TargetPointKey>(this, IN_PORT_TARGET_POINT));
            teleportRuntime.SetStopMovement(GetStopMovement());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (GetTargetSource() == SpawnPositionSource.TargetPoint &&
                context.GetInputValue<TargetPointKey>(IN_PORT_TARGET_POINT) == null) {
                context.Error("Teleport To target point key is required.");
            } else if (GetTargetSource() == SpawnPositionSource.TargetPoint) {
                context.ValidateTargetPointKey(IN_PORT_TARGET_POINT, "Teleport To target");
            }
        }

        private SpawnPositionSource GetTargetSource() {
            if (GetNodeOptionByName(OPTION_TARGET_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.TargetPoint;
        }

        private bool GetStopMovement() {
            if (GetNodeOptionByName(OPTION_STOP_MOVEMENT)?.TryGetValue(out bool stopMovement) == true)
                return stopMovement;

            return true;
        }
    }
}
