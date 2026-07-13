using System.Collections.Generic;
using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;
using Utility;

namespace TitanTool.Runtime.Nodes.Custom {
    public class ThrowState {
        public bool thrown;
    }

    [NodeView("Throw Object", "Action/")]
    public class ThrowNode : Node {
        [SerializeField] private GameObject m_prefab;
        [SerializeField] private SpawnPositionSource m_spawnSource = SpawnPositionSource.Boss;
        [SerializeField] private SpawnPositionSource m_targetSource = SpawnPositionSource.Player;
        [SerializeField] private RuntimeVector2Value m_spawnPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_targetPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_spawnOffset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_targetOffset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeTargetPointKeyValue m_spawnPointKey;
        [SerializeField] private RuntimeTargetPointKeyValue m_targetSpawnPointKey;
        [SerializeField] private RuntimeFloatValue m_flightTime = RuntimeFloatValue.Fixed(1f);
        [SerializeField] private RuntimeFloatValue m_angularVelocity = RuntimeFloatValue.Fixed(0f);
        [SerializeField] private DamagableTeam m_ownerTeam = DamagableTeam.Opponent;

        public void SetPrefab(GameObject prefab) => m_prefab = prefab;
        public void SetSpawnSource(SpawnPositionSource source) => m_spawnSource = source;
        public void SetTargetSource(SpawnPositionSource source) => m_targetSource = source;
        public void SetSpawnPosition(Vector2 position) => m_spawnPosition = RuntimeVector2Value.Fixed(position);
        public void SetSpawnPosition(RuntimeVector2Value position) => m_spawnPosition = position;
        public void SetTargetPosition(Vector2 position) => m_targetPosition = RuntimeVector2Value.Fixed(position);
        public void SetTargetPosition(RuntimeVector2Value position) => m_targetPosition = position;
        public void SetSpawnOffset(Vector2 offset) => m_spawnOffset = RuntimeVector2Value.Fixed(offset);
        public void SetSpawnOffset(RuntimeVector2Value offset) => m_spawnOffset = offset;
        public void SetTargetOffset(Vector2 offset) => m_targetOffset = RuntimeVector2Value.Fixed(offset);
        public void SetTargetOffset(RuntimeVector2Value offset) => m_targetOffset = offset;
        public void SetSpawnPointKey(TargetPointKey key) => m_spawnPointKey = RuntimeTargetPointKeyValue.Fixed(key);
        public void SetSpawnPointKey(RuntimeTargetPointKeyValue key) => m_spawnPointKey = key;
        public void SetTargetSpawnPointKey(TargetPointKey key) => m_targetSpawnPointKey = RuntimeTargetPointKeyValue.Fixed(key);
        public void SetTargetSpawnPointKey(RuntimeTargetPointKeyValue key) => m_targetSpawnPointKey = key;
        public void SetFlightTime(float flightTime) => m_flightTime = RuntimeFloatValue.Fixed(Mathf.Max(0.05f, flightTime));
        public void SetFlightTime(RuntimeFloatValue flightTime) => m_flightTime = flightTime;
        public void SetAngularVelocity(float angularVelocity) => m_angularVelocity = RuntimeFloatValue.Fixed(angularVelocity);
        public void SetAngularVelocity(RuntimeFloatValue angularVelocity) => m_angularVelocity = angularVelocity;
        public void SetOwnerTeam(DamagableTeam ownerTeam) => m_ownerTeam = ownerTeam;

        public override NodeStatus Tick(NodeContext ctx) {
            ThrowState state = ctx.GetState<ThrowState>(this);
            if (state.thrown)
                return NodeStatus.Success;

            if (m_prefab == null) {
                Debug.LogError($"{name}: Throw node requires a prefab.");
                return NodeStatus.Failure;
            }

            if (!TryResolvePosition(ctx, m_spawnSource, m_spawnPosition.Evaluate(), m_spawnPointKey.Evaluate(), m_spawnOffset.Evaluate(), out Vector2 spawnPosition) ||
                !TryResolvePosition(ctx, m_targetSource, m_targetPosition.Evaluate(), m_targetSpawnPointKey.Evaluate(), m_targetOffset.Evaluate(), out Vector2 targetPosition)) {
                Debug.LogError($"{name}: Throw node could not resolve spawn or target position.");
                return NodeStatus.Failure;
            }

            GameObject obj = Instantiate(m_prefab, spawnPosition, Quaternion.identity);
            if (obj == null)
                return NodeStatus.Failure;

            if (!obj.TryGetComponent(out Rigidbody2D rb)) {
                Debug.LogError($"{name}: Thrown prefab requires a Rigidbody2D.", obj);
                Destroy(obj);
                return NodeStatus.Failure;
            }

            if (!obj.TryGetComponent(out Collider2D _)) {
                Debug.LogError($"{name}: Thrown prefab requires a Collider2D.", obj);
                Destroy(obj);
                return NodeStatus.Failure;
            }

            float time = Mathf.Max(0.05f, m_flightTime.Evaluate());
            Vector2 displacement = targetPosition - spawnPosition;
            Vector2 gravity = Physics2D.gravity * rb.gravityScale;
            obj.SendMessage("SetOwnerTeam", m_ownerTeam, SendMessageOptions.DontRequireReceiver);
            rb.linearVelocity = displacement / time - 0.5f * gravity * time;
            rb.angularVelocity = m_angularVelocity.Evaluate();

            state.thrown = true;
            return NodeStatus.Success;
        }

        private bool TryResolvePosition(
            NodeContext ctx,
            SpawnPositionSource source,
            Vector2 fixedPosition,
            TargetPointKey spawnPointKey,
            Vector2 offset,
            out Vector2 position
        ) {
            position = fixedPosition;

            switch (source) {
                case SpawnPositionSource.FixedPosition:
                    break;

                case SpawnPositionSource.Player:
                    if (!ctx.blackboard.TryGet(BKeys.PlayerTransform, out Transform player) || player == null)
                        return false;
                    position = player.position;
                    break;

                case SpawnPositionSource.Boss:
                    if (!ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) || boss == null)
                        return false;
                    position = boss.position;
                    break;

                case SpawnPositionSource.TargetPoint:
                    if (!SpawnPointResolver.TryResolveByKey(ctx, spawnPointKey, out position)) {
                        return false;
                    }
                    break;
            }

            position += offset;
            return true;
        }
    }
}
