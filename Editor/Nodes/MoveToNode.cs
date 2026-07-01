using System;
using TitanTool.Runtime;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.MoveToNode), "Move To Target", "Action/Movement/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Movement, "Moves the boss toward a player, point, or fixed position.")]
    public class MoveToNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_TARGET_POSITION = "TargetPosition";
        private const string IN_PORT_OFFSET = "Offset";
        private const string IN_PORT_SPAWN_POINT_KEY = "SpawnPointKey";
        private const string IN_PORT_SPEED = "Speed";
        private const string IN_PORT_STOP_DISTANCE = "StopDistance";
        private const string IN_PORT_TIMEOUT = "Timeout";
        private const string OPTION_TARGET_SOURCE = "TargetSource";
        private const string OPTION_STOP_ON_ARRIVAL = "StopOnArrival";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.MoveToNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<SpawnPositionSource>(OPTION_TARGET_SOURCE)
                .WithDisplayName("Target Source")
                .WithDefaultValue(SpawnPositionSource.Player)
                .Delayed();

            context.AddOption<bool>(OPTION_STOP_ON_ARRIVAL)
                .WithDisplayName("Stop On Arrival")
                .WithDefaultValue(true)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            SpawnPositionSource targetSource = GetTargetSource();
            switch (targetSource) {
                case SpawnPositionSource.FixedPosition:
                    context.AddInputPort<Vector2>(IN_PORT_TARGET_POSITION)
                        .WithDisplayName("World Position")
                        .WithDefaultValue(Vector2.zero)
                        .Build();
                    break;

                case SpawnPositionSource.TargetPoint:
                    context.AddInputPort<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY)
                        .WithDisplayName("Target Point Key")
                        .Build();
                    break;
            }

            context.AddInputPort<Vector2>(IN_PORT_OFFSET)
                .WithDisplayName("Target Offset")
                .WithDefaultValue(Vector2.zero)
                .Build();

            context.AddInputPort<float>(IN_PORT_SPEED)
                .WithDisplayName("Move Speed")
                .WithDefaultValue(4f)
                .Build();

            context.AddInputPort<float>(IN_PORT_STOP_DISTANCE)
                .WithDisplayName("Stop Distance")
                .WithDefaultValue(0.2f)
                .Build();

            context.AddInputPort<float>(IN_PORT_TIMEOUT)
                .WithDisplayName("Timeout Sec")
                .WithDefaultValue(0f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.MoveToNode moveRuntime)
                return;

            moveRuntime.SetTargetSource(GetTargetSource());
            moveRuntime.SetTargetPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_TARGET_POSITION));
            moveRuntime.SetOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_OFFSET));
            moveRuntime.SetSpawnPointKey(GraphNodePortUtility.GetInputValue<TargetPointKey>(this, IN_PORT_SPAWN_POINT_KEY));
            moveRuntime.SetSpeed(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_SPEED));
            moveRuntime.SetStopDistance(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_STOP_DISTANCE));
            moveRuntime.SetTimeout(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_TIMEOUT));
            moveRuntime.SetStopOnArrival(GetStopOnArrival());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<float>(IN_PORT_SPEED) <= 0f)
                context.Error("Move To speed must be greater than 0.");

            if (context.GetInputValue<float>(IN_PORT_STOP_DISTANCE) < 0f)
                context.Error("Move To stop distance cannot be negative.");

            if (context.GetInputValue<float>(IN_PORT_TIMEOUT) < 0f)
                context.Error("Move To timeout cannot be negative.");

            SpawnPositionSource targetSource = GetTargetSource();
            if (targetSource == SpawnPositionSource.TargetPoint && context.GetInputValue<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY) == null)
                context.Error("Move To target point key is required.");
        }

        private SpawnPositionSource GetTargetSource() {
            if (GetNodeOptionByName(OPTION_TARGET_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.Player;
        }

        private bool GetStopOnArrival() {
            if (GetNodeOptionByName(OPTION_STOP_ON_ARRIVAL)?.TryGetValue(out bool stopOnArrival) == true)
                return stopOnArrival;

            return true;
        }
    }
}
