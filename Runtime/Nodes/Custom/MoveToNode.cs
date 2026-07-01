using System.Collections.Generic;
using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public class MoveToState {
        public float elapsed;
        public float speed;
        public float stopDistance;
        public float timeout;
        public Vector2 targetPosition;
        public Vector2 offset;
        public TargetPointKey spawnPointKey;
    }

    [NodeView("Move To Target", "Action/Movement/")]
    public class MoveToNode : ActionNode {
        [SerializeField] private SpawnPositionSource m_targetSource = SpawnPositionSource.Player;
        [SerializeField] private RuntimeVector2Value m_targetPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_offset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private TargetPointKey m_spawnPointKey;
        [SerializeField] private RuntimeFloatValue m_speed = RuntimeFloatValue.Fixed(4f);
        [SerializeField] private RuntimeFloatValue m_stopDistance = RuntimeFloatValue.Fixed(0.2f);
        [SerializeField] private RuntimeFloatValue m_timeout = RuntimeFloatValue.Fixed(0f);
        [SerializeField] private bool m_stopOnArrival = true;

        public void SetTargetSource(SpawnPositionSource targetSource) => m_targetSource = targetSource;
        public void SetTargetPosition(Vector2 targetPosition) => m_targetPosition = RuntimeVector2Value.Fixed(targetPosition);
        public void SetTargetPosition(RuntimeVector2Value targetPosition) => m_targetPosition = targetPosition;
        public void SetOffset(Vector2 offset) => m_offset = RuntimeVector2Value.Fixed(offset);
        public void SetOffset(RuntimeVector2Value offset) => m_offset = offset;
        public void SetSpawnPointKey(TargetPointKey spawnPointKey) => m_spawnPointKey = spawnPointKey;
        public void SetSpeed(float speed) => m_speed = RuntimeFloatValue.Fixed(Mathf.Max(0f, speed));
        public void SetSpeed(RuntimeFloatValue speed) => m_speed = speed;
        public void SetStopDistance(float stopDistance) => m_stopDistance = RuntimeFloatValue.Fixed(Mathf.Max(0f, stopDistance));
        public void SetStopDistance(RuntimeFloatValue stopDistance) => m_stopDistance = stopDistance;
        public void SetTimeout(float timeout) => m_timeout = RuntimeFloatValue.Fixed(Mathf.Max(0f, timeout));
        public void SetTimeout(RuntimeFloatValue timeout) => m_timeout = timeout;
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
                state.timeout = Mathf.Max(0f, m_timeout.Evaluate());
                state.targetPosition = m_targetPosition.Evaluate();
                state.offset = m_offset.Evaluate();
                state.spawnPointKey = m_spawnPointKey;
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
            if (toTarget.magnitude <= state.stopDistance) {
                if (m_stopOnArrival) {
                    rb.position = target;
                    Stop(rb);
                }

                ctx.ResetNode(this);
                ctx.SetStatus(this, NodeStatus.Success);
                return NodeStatus.Success;
            }

            if (state.timeout > 0f && state.elapsed >= state.timeout) {
                if (m_stopOnArrival)
                    Stop(rb);

                ctx.ResetNode(this);
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            float maxSpeedThisTick = toTarget.magnitude / Mathf.Max(ctx.deltaTime, Time.fixedDeltaTime);
            rb.linearVelocity = toTarget.normalized * Mathf.Min(state.speed, maxSpeedThisTick);
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
                    if (!SpawnPointResolver.TryResolveByKey(ctx, state.spawnPointKey, out target))
                        return false;
                    break;
            }

            target += state.offset;
            return true;
        }

        private static void Stop(Rigidbody2D rb) {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}
