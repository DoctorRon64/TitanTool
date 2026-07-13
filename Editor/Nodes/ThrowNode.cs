using System;
using TitanTool.Runtime;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using Utility;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.ThrowNode), "Throw Object", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Spawn, "Launches a Rigidbody2D prefab from one source toward another.")]
    public class ThrowNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_PREFAB = "Prefab";
        private const string IN_PORT_SPAWN_POSITION = "SpawnPosition";
        private const string IN_PORT_TARGET_POSITION = "TargetPosition";
        private const string IN_PORT_SPAWN_OFFSET = "SpawnOffset";
        private const string IN_PORT_TARGET_OFFSET = "TargetOffset";
        private const string IN_PORT_SPAWN_POINT_KEY = "SpawnPointKey";
        private const string IN_PORT_TARGET_SPAWN_POINT_KEY = "TargetSpawnPointKey";
        private const string IN_PORT_FLIGHT_TIME = "FlightTime";
        private const string IN_PORT_ANGULAR_VELOCITY = "AngularVelocity";
        private const string OPTION_SPAWN_SOURCE = "SpawnSource";
        private const string OPTION_TARGET_SOURCE = "TargetSource";
        private const string OPTION_OWNER_TEAM = "OwnerTeam";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.ThrowNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<SpawnPositionSource>(OPTION_SPAWN_SOURCE)
                .WithDisplayName("Launch From")
                .WithDefaultValue(SpawnPositionSource.Boss)
                .Delayed();

            context.AddOption<SpawnPositionSource>(OPTION_TARGET_SOURCE)
                .WithDisplayName("Land At")
                .WithDefaultValue(SpawnPositionSource.Player)
                .Delayed();

            context.AddOption<DamagableTeam>(OPTION_OWNER_TEAM)
                .WithDisplayName("Thrown Owner")
                .WithDefaultValue(DamagableTeam.Opponent)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<GameObject>(IN_PORT_PREFAB)
                .WithDisplayName("Object Prefab")
                .Build();

            AddPositionPorts(context, GetSpawnSource(), IN_PORT_SPAWN_POSITION, IN_PORT_SPAWN_POINT_KEY, "Spawn");
            AddPositionPorts(context, GetTargetSource(), IN_PORT_TARGET_POSITION, IN_PORT_TARGET_SPAWN_POINT_KEY, "Target");

            context.AddInputPort<Vector2>(IN_PORT_SPAWN_OFFSET)
                .WithDisplayName("Spawn Offset")
                .WithDefaultValue(Vector2.zero)
                .Build();

            context.AddInputPort<Vector2>(IN_PORT_TARGET_OFFSET)
                .WithDisplayName("Target Offset")
                .WithDefaultValue(Vector2.zero)
                .Build();

            context.AddInputPort<float>(IN_PORT_FLIGHT_TIME)
                .WithDisplayName("Flight Time")
                .WithDefaultValue(1f)
                .Build();

            context.AddInputPort<float>(IN_PORT_ANGULAR_VELOCITY)
                .WithDisplayName("Spin Speed")
                .WithDefaultValue(0f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.ThrowNode throwRuntime)
                return;

            throwRuntime.SetPrefab(GraphNodePortUtility.GetInputValue<GameObject>(this, IN_PORT_PREFAB));
            throwRuntime.SetSpawnSource(GetSpawnSource());
            throwRuntime.SetTargetSource(GetTargetSource());
            throwRuntime.SetSpawnPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_SPAWN_POSITION));
            throwRuntime.SetTargetPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_TARGET_POSITION));
            throwRuntime.SetSpawnOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_SPAWN_OFFSET));
            throwRuntime.SetTargetOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_TARGET_OFFSET));
            throwRuntime.SetSpawnPointKey(GraphNodePortUtility.GetInputValue<TargetPointKey>(this, IN_PORT_SPAWN_POINT_KEY));
            throwRuntime.SetTargetSpawnPointKey(GraphNodePortUtility.GetInputValue<TargetPointKey>(this, IN_PORT_TARGET_SPAWN_POINT_KEY));
            throwRuntime.SetFlightTime(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_FLIGHT_TIME));
            throwRuntime.SetAngularVelocity(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_ANGULAR_VELOCITY));
            throwRuntime.SetOwnerTeam(GetOwnerTeam());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            GameObject prefab = context.GetInputValue<GameObject>(IN_PORT_PREFAB);
            if (prefab == null) {
                context.Error("Throw node requires a prefab.");
            } else {
                if (prefab.GetComponent<Rigidbody2D>() == null)
                    context.Error("Throw prefab requires a Rigidbody2D.");
                if (prefab.GetComponent<Collider2D>() == null)
                    context.Error("Throw prefab requires a Collider2D.");
            }

            if (context.GetInputValue<float>(IN_PORT_FLIGHT_TIME) <= 0f)
                context.Error("Throw flight time must be greater than 0.");

            ValidatePositionSource(context, GetSpawnSource(), IN_PORT_SPAWN_POINT_KEY, "spawn");
            ValidatePositionSource(context, GetTargetSource(), IN_PORT_TARGET_SPAWN_POINT_KEY, "target");
        }

        private static void AddPositionPorts(
            IPortDefinitionContext context,
            SpawnPositionSource source,
            string positionPort,
            string spawnPointKeyPort,
            string label
        ) {
            switch (source) {
                case SpawnPositionSource.FixedPosition:
                    context.AddInputPort<Vector2>(positionPort)
                        .WithDisplayName($"{label} Position")
                        .WithDefaultValue(Vector2.zero)
                        .Build();
                    break;

                case SpawnPositionSource.TargetPoint:
                    context.AddInputPort<TargetPointKey>(spawnPointKeyPort)
                        .WithDisplayName($"{label} Point Key")
                        .Build();
                    break;
            }
        }

        private static void ValidatePositionSource(
            BossGraphNodeValidationContext context,
            SpawnPositionSource source,
            string spawnPointKeyPort,
            string label
        ) {
            if (source == SpawnPositionSource.TargetPoint && context.GetInputValue<TargetPointKey>(spawnPointKeyPort) == null)
                context.Error($"Throw {label} target point key is required.");
        }

        private SpawnPositionSource GetSpawnSource() {
            if (GetNodeOptionByName(OPTION_SPAWN_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.Boss;
        }

        private SpawnPositionSource GetTargetSource() {
            if (GetNodeOptionByName(OPTION_TARGET_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.Player;
        }

        private DamagableTeam GetOwnerTeam() {
            if (GetNodeOptionByName(OPTION_OWNER_TEAM)?.TryGetValue(out DamagableTeam team) == true)
                return team;

            return DamagableTeam.Opponent;
        }
    }
}
