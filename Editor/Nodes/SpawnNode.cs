using System;
using TitanTool.Runtime;
using TitanTool.Runtime.Nodes.Custom;
using UnityEditor;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.SpawnNode), "Spawn Object", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Spawn, "Creates a prefab at the boss, the player, a TargetPointKey, or a fixed world position.")]
    public class SpawnNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        public const string IN_PORT_PREFAB = "InPrefabPort";
        public const string IN_PORT_POSITION = "InPostionPOrt";
        public const string IN_PORT_OFFSET = "Offset";
        public const string IN_PORT_SPAWN_POINT_KEY = "SpawnPointKey";
        private const string OPTION_POSITION_SOURCE = "PositionSource";

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.SpawnNode));
        }

        protected override bool hasInput => true;
        protected override bool hasOutput => false;

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<SpawnPositionSource>(OPTION_POSITION_SOURCE)
                .WithDisplayName("Spawn From")
                .WithDefaultValue(SpawnPositionSource.FixedPosition)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            context.AddInputPort<GameObject>(IN_PORT_PREFAB)
                .WithDisplayName("Object Prefab")
                .Build();

            SpawnPositionSource source = GetPositionSource();
            switch (source) {
                case SpawnPositionSource.FixedPosition:
                    context.AddInputPort<Vector2>(IN_PORT_POSITION)
                        .WithDisplayName("World Position")
                        .WithDefaultValue(Vector2.zero)
                        .Build();
                    break;

                case SpawnPositionSource.TargetPoint:
                    context.AddInputPort<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY)
                        .WithDisplayName("Spawn Point Key")
                        .Build();
                    break;
            }

            context.AddInputPort<Vector2>(IN_PORT_OFFSET)
                .WithDisplayName("Spawn Offset")
                .WithDefaultValue(Vector2.zero)
                .Build();
        }

        [SerializeField] private string m_prefabPath;

        public void SetPrefab(GameObject prefab) {
            m_prefabPath = AssetDatabase.GetAssetPath(prefab);
        }

        public GameObject LoadPrefab() {
            return AssetDatabase.LoadAssetAtPath<GameObject>(m_prefabPath);
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.SpawnNode spawnRuntime) {
                return;
            }

            spawnRuntime.SetPrefab(GraphNodePortUtility.GetInputValue<GameObject>(this, IN_PORT_PREFAB));
            spawnRuntime.SetPosition(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_POSITION));
            spawnRuntime.SetOffset(GraphNodePortUtility.GetRuntimeVector2Value(this, IN_PORT_OFFSET));
            spawnRuntime.SetSpawnPointKey(GraphNodePortUtility.GetRuntimeTargetPointKeyValue(this, IN_PORT_SPAWN_POINT_KEY));
            spawnRuntime.SetPositionSource(GetPositionSource());
        }

        public void Validate(BossGraphNodeValidationContext context) {
            if (context.GetInputValue<GameObject>(IN_PORT_PREFAB) == null) {
                context.Error("Spawn node requires a prefab.");
            }

            SpawnPositionSource source = GetPositionSource();
            if (source == SpawnPositionSource.TargetPoint && context.GetInputValue<TargetPointKey>(IN_PORT_SPAWN_POINT_KEY) == null) {
                context.Error("Target Point key is required.");
            } else if (source == SpawnPositionSource.TargetPoint) {
                context.ValidateTargetPointKey(IN_PORT_SPAWN_POINT_KEY, "Spawn point");
            }
        }

        private SpawnPositionSource GetPositionSource() {
            if (GetNodeOptionByName(OPTION_POSITION_SOURCE)?.TryGetValue(out SpawnPositionSource source) == true)
                return source;

            return SpawnPositionSource.FixedPosition;
        }
    }
}
