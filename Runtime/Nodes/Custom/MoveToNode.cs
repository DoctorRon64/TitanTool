using System.Collections.Generic;
using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public enum MoveToMode {
        TowardTarget,
        AwayFromTarget
    }

    public enum MoveTargetPointUpdateMode {
        OnMoveStart,
        EveryTick
    }

    public class MoveToState {
        public float elapsed;
        public float speed;
        public float stopDistance;
        public MoveToMode moveMode;
        public Vector2 targetPosition;
        public Vector2 offset;
        public TargetPointKey spawnPointKey;
    }

    [NodeView("Move", "Action/Movement/")]
    public class MoveToNode : ActionNode {
        [SerializeField] private MoveToMode m_moveMode = MoveToMode.TowardTarget;
        [SerializeField] private SpawnPositionSource m_targetSource = SpawnPositionSource.Player;
        [SerializeField] private RuntimeVector2Value m_targetPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_offset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeTargetPointKeyValue m_spawnPointKey;
        [SerializeField] private MoveTargetPointUpdateMode m_targetPointUpdateMode = MoveTargetPointUpdateMode.OnMoveStart;
        [SerializeField] private RuntimeFloatValue m_speed = RuntimeFloatValue.Fixed(4f);
        [SerializeField] private RuntimeFloatValue m_stopDistance = RuntimeFloatValue.Fixed(0.2f);
        [SerializeField] private bool m_stopOnArrival = true;

        public void SetMoveMode(MoveToMode moveMode) => m_moveMode = moveMode;
        public void SetTargetSource(SpawnPositionSource targetSource) => m_targetSource = targetSource;
        public void SetTargetPosition(Vector2 targetPosition) => m_targetPosition = RuntimeVector2Value.Fixed(targetPosition);
        public void SetTargetPosition(RuntimeVector2Value targetPosition) => m_targetPosition = targetPosition;
        public void SetOffset(Vector2 offset) => m_offset = RuntimeVector2Value.Fixed(offset);
        public void SetOffset(RuntimeVector2Value offset) => m_offset = offset;
        public void SetSpawnPointKey(TargetPointKey spawnPointKey) => m_spawnPointKey = RuntimeTargetPointKeyValue.Fixed(spawnPointKey);
        public void SetSpawnPointKey(RuntimeTargetPointKeyValue spawnPointKey) => m_spawnPointKey = spawnPointKey;
        public void SetTargetPointUpdateMode(MoveTargetPointUpdateMode targetPointUpdateMode) => m_targetPointUpdateMode = targetPointUpdateMode;
        public void SetSpeed(float speed) => m_speed = RuntimeFloatValue.Fixed(Mathf.Max(0f, speed));
        public void SetSpeed(RuntimeFloatValue speed) => m_speed = speed;
        public void SetStopDistance(float stopDistance) => m_stopDistance = RuntimeFloatValue.Fixed(Mathf.Max(0f, stopDistance));
        public void SetStopDistance(RuntimeFloatValue stopDistance) => m_stopDistance = stopDistance;
        public void SetStopOnArrival(bool stopOnArrival) => m_stopOnArrival = stopOnArrival;

        public override NodeStatus Tick(NodeContext ctx) {
            if (!TryResolveRigidbody(ctx, out Rigidbody2D rb)) {
                Debug.LogError($"{name}: Move To requires a Rigidbody2D on the boss.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            MoveToState state = ctx.GetState<MoveToState>(this);
            if (state.elapsed <= 0f) {
                state.speed = Mathf.Max(0f, m_speed.Evaluate());
                state.stopDistance = Mathf.Max(0f, m_stopDistance.Evaluate());
                state.moveMode = m_moveMode;
                state.targetPosition = m_targetPosition.Evaluate();
                state.offset = m_offset.Evaluate();
                state.spawnPointKey = m_spawnPointKey.Evaluate();
            }

            if (!TryResolveTarget(ctx, state, out Vector2 target)) {
                Stop(rb);
                Debug.LogError($"{name}: Move To could not resolve target source {m_targetSource}.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            state.elapsed += ctx.deltaTime;

            Vector2 current = rb.position;
            Vector2 toTarget = target - current;
            float distanceToTarget = toTarget.magnitude;

            if (HasReachedDestination(state, distanceToTarget)) {
                if (m_stopOnArrival) {
                    if (state.moveMode == MoveToMode.TowardTarget)
                        rb.position = target;

                    Stop(rb);
                }

                ctx.ResetNode(this);
                ctx.SetStatus(this, NodeStatus.Success);
                return NodeStatus.Success;
            }

            Vector2 moveDirection = GetMoveDirection(state, rb, toTarget);
            float speed = state.moveMode == MoveToMode.TowardTarget
                ? Mathf.Min(state.speed, distanceToTarget / Mathf.Max(ctx.deltaTime, Time.fixedDeltaTime))
                : state.speed;

            rb.linearVelocity = moveDirection * speed;
            ctx.SetStatus(this, NodeStatus.Running);
            return NodeStatus.Running;
        }

        public override void Abort(NodeContext ctx) {
            if (TryResolveRigidbody(ctx, out Rigidbody2D rb))
                Stop(rb);
        }

        private bool TryResolveRigidbody(NodeContext ctx, out Rigidbody2D rb) {
            if (ctx.blackboard.TryGet(BKeys.BossRigidbody2D, out rb) && rb != null)
                return true;

            rb = null;
            if (ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) && boss != null)
                return boss.TryGetComponent(out rb);

            return false;
        }

        private bool TryResolveTarget(NodeContext ctx, MoveToState state, out Vector2 target) {
            target = state.targetPosition;

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
                    TargetPointKey spawnPointKey = m_targetPointUpdateMode == MoveTargetPointUpdateMode.EveryTick
                        ? m_spawnPointKey.Evaluate()
                        : state.spawnPointKey;

                    if (!SpawnPointResolver.TryResolveByKey(ctx, spawnPointKey, out target))
                        return false;
                    break;
            }

            target += state.offset;
            return true;
        }

        private static bool HasReachedDestination(MoveToState state, float distanceToTarget) {
            return state.moveMode == MoveToMode.AwayFromTarget
                ? state.stopDistance > 0f && distanceToTarget >= state.stopDistance
                : distanceToTarget <= state.stopDistance;
        }

        private static Vector2 GetMoveDirection(MoveToState state, Rigidbody2D rb, Vector2 toTarget) {
            Vector2 direction = state.moveMode == MoveToMode.AwayFromTarget
                ? -toTarget
                : toTarget;

            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;

            if (rb.linearVelocity.sqrMagnitude > 0.0001f)
                return rb.linearVelocity.normalized;

            Vector2 right = rb.transform.right;
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector2.right;
        }

        private static void Stop(Rigidbody2D rb) {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}
