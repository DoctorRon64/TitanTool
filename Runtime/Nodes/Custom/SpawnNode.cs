using System.Collections.Generic;
using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public class SpawnState {
        public bool spawned;
    }

    [NodeView("Spawn Object", "Action/")]
    public class SpawnNode : Node {
        [SerializeField] private GameObject m_prefab;
        [SerializeField] private RuntimeVector2Value m_spawnPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_offset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private TargetPointKey m_spawnPointKey;
        [SerializeField] private SpawnPositionSource m_positionSource;
        
        public void SetPrefab(GameObject prefab) => m_prefab = prefab;
        public void SetPosition(Vector2 position) => m_spawnPosition = RuntimeVector2Value.Fixed(position);
        public void SetPosition(RuntimeVector2Value position) => m_spawnPosition = position;
        public void SetOffset(Vector2 offset) => m_offset = RuntimeVector2Value.Fixed(offset);
        public void SetOffset(RuntimeVector2Value offset) => m_offset = offset;
        public void SetSpawnPointKey(TargetPointKey spawnPointKey) => m_spawnPointKey = spawnPointKey;
        public void SetPositionSource(SpawnPositionSource source) => m_positionSource = source;

        public override NodeStatus Tick(NodeContext ctx) {
            if (m_prefab == null) {
                Debug.LogError($"{name}: Missing prefab");
                return NodeStatus.Failure;
            }

            Vector3 spawnPosition = m_spawnPosition.Evaluate();
            Vector2 offset = m_offset.Evaluate();
            switch (m_positionSource) {
                case SpawnPositionSource.Player:
                    Transform player = ctx.blackboard.Get(BKeys.PlayerTransform);
                    if (player == null) {
                        Debug.LogError("SpawnNode: PlayerTransform is missing from blackboard.");
                        return NodeStatus.Failure;
                    }
                    spawnPosition = player.position;
                    break;

                case SpawnPositionSource.Boss:
                    Transform boss = ctx.blackboard.Get(BKeys.BossTransform);
                    if (boss == null) {
                        Debug.LogError("SpawnNode: BossTransform is missing from blackboard.");
                        return NodeStatus.Failure;
                    }
                    spawnPosition = boss.position;
                    break;

                case SpawnPositionSource.TargetPoint:
                    if (!SpawnPointResolver.TryResolveByKey(ctx, m_spawnPointKey, out Vector2 keyedSpawnPosition)) {
                        Debug.LogError($"SpawnNode: Target point key '{(m_spawnPointKey != null ? m_spawnPointKey.name : "None")}' was not found.");
                        return NodeStatus.Failure;
                    }

                    spawnPosition = keyedSpawnPosition;
                    break;
            }

            spawnPosition += (Vector3)offset;

            SpawnState state = ctx.GetState<SpawnState>(this);

            if (state.spawned) {
                ctx.SetStatus(this, NodeStatus.Success);
                return NodeStatus.Success;
            }

            state.spawned = true;
            GameObject obj = Instantiate(m_prefab, spawnPosition, Quaternion.identity);

            NodeStatus status = obj != null
                ? NodeStatus.Success
                : NodeStatus.Failure;
            if (status == NodeStatus.Success) {
                ctx.ResetNode(this);
            }

            ctx.SetStatus(this, status);
            return status;
        }
        
    }
    
    public enum SpawnPositionSource {
        FixedPosition,
        Player,
        Boss,
        TargetPoint
    }
}
