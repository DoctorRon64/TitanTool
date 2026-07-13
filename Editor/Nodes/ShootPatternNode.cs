using System;
using TitanTool.Runtime;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using Utility;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.ShootPatternNode), "Fire Bullets", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Shoot, "Spawns bullets in the selected pattern from the chosen source.")]
    public class ShootPatternNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_BULLET_PREFAB = "BulletPrefab";
        private const string IN_PORT_POSITION = "Position";
        private const string IN_PORT_DIRECTION = "Direction";
        private const string IN_PORT_OFFSET = "Offset";
        private const string IN_PORT_SPAWN_POINT_KEY = "SpawnPointKey";
        private const string IN_PORT_TARGET_TRANSFORM = "TargetTransform";
        private const string IN_PORT_BULLET_COUNT = "BulletCount";
        private const string IN_PORT_SPREAD_ANGLE = "SpreadAngle";
        private const string IN_PORT_SPEED = "Speed";
        private const string OPTION_PATTERN = "Pattern";
        private const string OPTION_POSITION_SOURCE = "PositionSource";
        private const string OPTION_AIM_SOURCE = "AimSource";
        private const string OPTION_OWNER_TEAM = "OwnerTeam";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.ShootPatternNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<ShootPattern>(OPTION_PATTERN)
                .WithDisplayName("Pattern")
                .WithDefaultValue(ShootPattern.Single)
                .Delayed();

            context.AddOption<SpawnPositionSource>(OPTION_POSITION_SOURCE)
                .WithDisplayName("Fire From")
                .WithDefaultValue(SpawnPositionSource.Boss)
                .Delayed();

            context.AddOption<ShootAimSource>(OPTION_AIM_SOURCE)
                .WithDisplayName("Aim At")
                .WithDefaultValue(ShootAimSource.FixedDirection)
                .Delayed();

            context.AddOption<DamagableTeam>(OPTION_OWNER_TEAM)
                .WithDisplayName("Owner Team")
                .WithDefaultValue(DamagableTeam.Opponent)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<GameObject>(IN_PORT_BULLET_PREFAB)
                .WithDisplayName("Bullet Prefab")
                .Build();

            SpawnPositionSource positionSource = GetPositionSource();
            switch (positionSource) {
                case SpawnPositionSource.FixedPosition:
                    context.AddInputPort<Vector2>(IN_PORT_POSITION)
                        .WithDisplayName("Fire Position")
                        .WithDefaultValue(Vector2.zero)
                        .Build();
                    break;

                case SpawnPositionSource.TargetPoint:
                    context.AddInputPort<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY)
                        .WithDisplayName("Spawn Point Key")
                        .Build();
                    break;
            }

            ShootAimSource aimSource = GetAimSource();
            if (aimSource == ShootAimSource.FixedDirection) {
                context.AddInputPort<Vector2>(IN_PORT_DIRECTION)
                    .WithDisplayName("Aim Direction")
                    .WithDefaultValue(Vector2.left)
                    .Build();
            }
            else if (aimSource == ShootAimSource.TargetTransform) {
                context.AddInputPort<Transform>(IN_PORT_TARGET_TRANSFORM)
                    .WithDisplayName("Aim Target")
                    .Build();
            }

            context.AddInputPort<Vector2>(IN_PORT_OFFSET)
                .WithDisplayName("Spawn Offset")
                .WithDefaultValue(Vector2.zero)
                .Build();

            context.AddInputPort<int>(IN_PORT_BULLET_COUNT)
                .WithDisplayName("Bullets")
                .WithDefaultValue(1)
                .Build();

            context.AddInputPort<float>(IN_PORT_SPREAD_ANGLE)
                .WithDisplayName("Spread")
                .WithDefaultValue(45f)
                .Build();

            context.AddInputPort<float>(IN_PORT_SPEED)
                .WithDisplayName("Bullet Speed")
                .WithDefaultValue(8f)
                .Build();
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.ShootPatternNode shootRuntime)
                return;

            shootRuntime.SetBulletPrefab(GraphNodePortUtility.GetInputValue<GameObject>(this, IN_PORT_BULLET_PREFAB));
            shootRuntime.SetPattern(GetPattern());
            shootRuntime.SetPositionSource(GetPositionSource());
            shootRuntime.SetAimSource(GetAimSource());
            shootRuntime.SetPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_POSITION));
            shootRuntime.SetDirection(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_DIRECTION));
            shootRuntime.SetOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_OFFSET));
            shootRuntime.SetSpawnPointKey(GraphNodePortUtility.GetInputValue<TargetPointKey>(this, IN_PORT_SPAWN_POINT_KEY));
            shootRuntime.SetTargetTransform(GraphNodePortUtility.GetInputValue<Transform>(this, IN_PORT_TARGET_TRANSFORM));
            shootRuntime.SetBulletCount(GraphNodePortUtility.GetRuntimeIntValue(this, IN_PORT_BULLET_COUNT));
            shootRuntime.SetSpreadAngle(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_SPREAD_ANGLE));
            shootRuntime.SetSpeed(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_SPEED));
            shootRuntime.SetOwnerTeam(GetOwnerTeam());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<GameObject>(IN_PORT_BULLET_PREFAB) == null)
                context.Error("Shoot Pattern requires a bullet prefab.");

            if (context.GetInputValue<int>(IN_PORT_BULLET_COUNT) <= 0)
                context.Error("Shoot Pattern bullet count must be greater than 0.");

            if (context.GetInputValue<float>(IN_PORT_SPEED) <= 0f)
                context.Error("Shoot Pattern speed must be greater than 0.");

            if (GetPositionSource() == SpawnPositionSource.TargetPoint && context.GetInputValue<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY) == null)
                context.Error("Shoot Pattern target point key is required.");

            if (GetAimSource() == ShootAimSource.TargetTransform && context.GetInputValue<Transform>(IN_PORT_TARGET_TRANSFORM) == null)
                context.Error("Shoot Pattern target transform is required when Aim Target is Target Transform.");
        }

        private ShootPattern GetPattern() {
            if (GetNodeOptionByName(OPTION_PATTERN)?.TryGetValue(out ShootPattern pattern) == true)
                return pattern;

            return ShootPattern.Single;
        }

        private SpawnPositionSource GetPositionSource() {
            if (GetNodeOptionByName(OPTION_POSITION_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.Boss;
        }

        private ShootAimSource GetAimSource() {
            if (GetNodeOptionByName(OPTION_AIM_SOURCE)?.TryGetValue(out ShootAimSource source) == true)
                return source;

            return ShootAimSource.FixedDirection;
        }

        private DamagableTeam GetOwnerTeam() {
            if (GetNodeOptionByName(OPTION_OWNER_TEAM)?.TryGetValue(out DamagableTeam team) == true)
                return team;

            return DamagableTeam.Opponent;
        }
    }
}
