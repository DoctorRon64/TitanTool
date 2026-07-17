using System;
using TitanTool.Runtime;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.MoveToNode), "Move", "Action/Movement/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Movement, "Moves the boss toward the target with positive speed or away from it with negative speed until the stop distance is reached.")]
    public class MoveToNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_TARGET_POSITION = "TargetPosition";
        private const string IN_PORT_OFFSET = "Offset";
        private const string IN_PORT_SPAWN_POINT_KEY = "SpawnPointKey";
        private const string IN_PORT_SPEED = "Speed";
        private const string IN_PORT_STOP_DISTANCE = "StopDistance";
        private const string OPTION_TARGET_SOURCE = "TargetSource";
        private const string OPTION_TARGET_POINT_UPDATE_MODE = "TargetPointUpdateMode";
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

            context.AddOption<MoveTargetPointUpdateMode>(OPTION_TARGET_POINT_UPDATE_MODE)
                .WithDisplayName("Target Point Update")
                .WithDefaultValue(MoveTargetPointUpdateMode.OnMoveStart)
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
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.MoveToNode moveRuntime)
                return;

            moveRuntime.SetTargetSource(GetTargetSource());
            moveRuntime.SetTargetPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_TARGET_POSITION));
            moveRuntime.SetOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_OFFSET));
            moveRuntime.SetSpawnPointKey(GraphNodePortUtility.GetRuntimeTargetPointKeyValue(this, IN_PORT_SPAWN_POINT_KEY));
            moveRuntime.SetTargetPointUpdateMode(GetTargetPointUpdateMode());
            moveRuntime.SetSpeed(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_SPEED));
            moveRuntime.SetStopDistance(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_STOP_DISTANCE));
            moveRuntime.SetStopOnArrival(GetStopOnArrival());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            float speed = context.GetInputValue<float>(IN_PORT_SPEED);
            if (Mathf.Approximately(speed, 0f))
                context.Error("Move speed cannot be 0. Use a positive speed to move toward the target or a negative speed to move away.");

            float stopDistance = context.GetInputValue<float>(IN_PORT_STOP_DISTANCE);
            if (stopDistance < 0f)
                context.Error("Move distance cannot be negative.");

            if (speed < 0f && stopDistance <= 0f)
                context.Error("Moving away requires a stop distance greater than 0.");

            SpawnPositionSource targetSource = GetTargetSource();
            if (targetSource == SpawnPositionSource.TargetPoint) {
                if (context.GetInputValue<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY) == null)
                    context.Error("Move target point key is required.");
                else
                    context.ValidateTargetPointKey(IN_PORT_SPAWN_POINT_KEY, "Move target");
            }
        }

        private SpawnPositionSource GetTargetSource() {
            if (GetNodeOptionByName(OPTION_TARGET_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.Player;
        }

        private MoveTargetPointUpdateMode GetTargetPointUpdateMode() {
            if (GetNodeOptionByName(OPTION_TARGET_POINT_UPDATE_MODE)?.TryGetValue(out MoveTargetPointUpdateMode updateMode) == true)
                return updateMode;

            return MoveTargetPointUpdateMode.OnMoveStart;
        }

        private bool GetStopOnArrival() {
            if (GetNodeOptionByName(OPTION_STOP_ON_ARRIVAL)?.TryGetValue(out bool stopOnArrival) == true)
                return stopOnArrival;

            return true;
        }
    }
}
