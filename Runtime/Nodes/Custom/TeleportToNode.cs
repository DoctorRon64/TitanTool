using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    [NodeView("Teleport To Target", "Action/Movement/")]
    public class TeleportToNode : ActionNode {
        [SerializeField] private SpawnPositionSource m_targetSource = SpawnPositionSource.Player;
        [SerializeField] private RuntimeVector2Value m_targetPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_offset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeTargetPointKeyValue m_spawnPointKey;
        [SerializeField] private bool m_stopMovement = true;

        public void SetTargetSource(SpawnPositionSource targetSource) => m_targetSource = targetSource;
        public void SetTargetPosition(RuntimeVector2Value targetPosition) => m_targetPosition = targetPosition;
        public void SetOffset(RuntimeVector2Value offset) => m_offset = offset;
        public void SetSpawnPointKey(TargetPointKey spawnPointKey) => m_spawnPointKey = RuntimeTargetPointKeyValue.Fixed(spawnPointKey);
        public void SetSpawnPointKey(RuntimeTargetPointKeyValue spawnPointKey) => m_spawnPointKey = spawnPointKey;
        public void SetStopMovement(bool stopMovement) => m_stopMovement = stopMovement;

        public override NodeStatus Tick(NodeContext ctx) {
            if (!TryResolveTarget(ctx, out Vector2 target)) {
                Debug.LogError($"{name}: Teleport To could not resolve target source {m_targetSource}.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            if (!ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) || boss == null) {
                Debug.LogError($"{name}: Teleport To requires BossTransform in the blackboard.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            if (TryResolveRigidbody(ctx, boss, out Rigidbody2D rb)) {
                rb.position = target;
                if (m_stopMovement) {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            } else {
                boss.position = target;
            }

            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }

        private bool TryResolveTarget(NodeContext ctx, out Vector2 target) {
            target = m_targetPosition.Evaluate();

            switch (m_targetSource) {
                case SpawnPositionSource.FixedPosition:
                    break;

                case SpawnPositionSource.Player:
                    if (!ctx.blackboard.TryGet(BKeys.PlayerTransform, out Transform player) || player == null)
                        return false;
                    target = player.position;
                    break;

                case SpawnPositionSource.Boss:
                    if (!ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) || boss == null)
                        return false;
                    target = boss.position;
                    break;

                case SpawnPositionSource.TargetPoint:
                    if (!SpawnPointResolver.TryResolveByKey(ctx, m_spawnPointKey.Evaluate(), out target))
                        return false;
                    break;
            }

            target += m_offset.Evaluate();
            return true;
        }

        private static bool TryResolveRigidbody(NodeContext ctx, Transform boss, out Rigidbody2D rb) {
            if (ctx.blackboard.TryGet(BKeys.BossRigidbody2D, out rb) && rb != null)
                return true;

            return boss.TryGetComponent(out rb);
        }
    }
}
