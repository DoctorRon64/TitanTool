using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;
using Utility;

namespace TitanTool.Runtime.Nodes.Custom {
    public enum ShootPattern {
        Single = 0,
        Spread = 1,
        AimedAtPlayer = 3
    }

    public enum ShootAimSource {
        FixedDirection,
        Player,
        Boss,
        TargetTransform,
        PlayerSnapshot
    }

    public class ShootState {
        public bool fired;
    }

    [NodeView("Shoot", "Action/")]
    public class ShootNode : ActionNode {
        private const string PlayerSnapshotKey = "__Shoot_PlayerSnapshot";

        [SerializeField] private GameObject m_bulletPrefab;
        [SerializeField] private ShootPattern m_pattern = ShootPattern.Single;
        [SerializeField] private SpawnPositionSource m_positionSource = SpawnPositionSource.Boss;
        [SerializeField] private ShootAimSource m_aimSource = ShootAimSource.FixedDirection;
        [SerializeField] private RuntimeVector2Value m_spawnPosition = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private RuntimeVector2Value m_direction = RuntimeVector2Value.Fixed(Vector2.left);
        [SerializeField] private RuntimeVector2Value m_offset = RuntimeVector2Value.Fixed(Vector2.zero);
        [SerializeField] private TargetPointKey m_spawnPointKey;
        [SerializeField] private Transform m_targetTransform;
        [SerializeField] private RuntimeIntValue m_bulletCount = RuntimeIntValue.Fixed(1);
        [SerializeField] private RuntimeFloatValue m_spreadAngle = RuntimeFloatValue.Fixed(45f);
        [SerializeField] private RuntimeFloatValue m_speed = RuntimeFloatValue.Fixed(8f);
        [SerializeField] private DamagableTeam m_ownerTeam = DamagableTeam.Opponent;

        public void SetBulletPrefab(GameObject bulletPrefab) => m_bulletPrefab = bulletPrefab;
        public void SetPattern(ShootPattern pattern) => m_pattern = (int)pattern == 2 ? ShootPattern.Spread : pattern;
        public void SetPositionSource(SpawnPositionSource positionSource) => m_positionSource = positionSource;
        public void SetAimSource(ShootAimSource aimSource) => m_aimSource = aimSource;
        public void SetPosition(Vector2 position) => m_spawnPosition = RuntimeVector2Value.Fixed(position);
        public void SetPosition(RuntimeVector2Value position) => m_spawnPosition = position;
        public void SetDirection(Vector2 direction) => m_direction = RuntimeVector2Value.Fixed(direction);
        public void SetDirection(RuntimeVector2Value direction) => m_direction = direction;
        public void SetOffset(Vector2 offset) => m_offset = RuntimeVector2Value.Fixed(offset);
        public void SetOffset(RuntimeVector2Value offset) => m_offset = offset;
        public void SetSpawnPointKey(TargetPointKey spawnPointKey) => m_spawnPointKey = spawnPointKey;
        public void SetTargetTransform(Transform targetTransform) => m_targetTransform = targetTransform;
        public void SetBulletCount(int bulletCount) => m_bulletCount = RuntimeIntValue.Fixed(Mathf.Max(1, bulletCount));
        public void SetBulletCount(RuntimeIntValue bulletCount) => m_bulletCount = bulletCount;
        public void SetSpreadAngle(float spreadAngle) => m_spreadAngle = RuntimeFloatValue.Fixed(Mathf.Max(0f, spreadAngle));
        public void SetSpreadAngle(RuntimeFloatValue spreadAngle) => m_spreadAngle = spreadAngle;
        public void SetSpeed(float speed) => m_speed = RuntimeFloatValue.Fixed(speed);
        public void SetSpeed(RuntimeFloatValue speed) => m_speed = speed;
        public void SetOwnerTeam(DamagableTeam ownerTeam) => m_ownerTeam = ownerTeam;

        public static void ClearPlayerSnapshots(NodeContext ctx) {
            ctx.blackboard.RemoveValue(PlayerSnapshotKey);
        }

        public override NodeStatus Tick(NodeContext ctx) {
            ShootState state = ctx.GetState<ShootState>(this);
            if (state.fired) {
                ctx.SetStatus(this, NodeStatus.Success);
                return NodeStatus.Success;
            }

            if (m_bulletPrefab == null) {
                Debug.LogError($"{name}: Missing bullet prefab.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            Vector3 origin = ResolvePosition(ctx);
            float speed = Mathf.Max(0f, m_speed.Evaluate());
            foreach (Vector2 direction in GetDirections(ctx, origin))
                SpawnBullet(origin, direction, speed);

            state.fired = true;
            ctx.ResetNode(this);
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }

        private System.Collections.Generic.IEnumerable<Vector2> GetDirections(NodeContext ctx, Vector3 origin) {
            Vector2 baseDirection = GetBaseDirection(ctx, origin);

            switch (m_pattern) {
                case ShootPattern.Spread:
                    int spreadCount = Mathf.Max(1, m_bulletCount.Evaluate());
                    if (spreadCount == 1) {
                        yield return baseDirection;
                        break;
                    }

                    float spreadAngle = Mathf.Max(0f, m_spreadAngle.Evaluate());
                    float start = -spreadAngle * 0.5f;
                    float step = spreadAngle / (spreadCount - 1);
                    for (int i = 0; i < spreadCount; i++)
                        yield return Rotate(baseDirection, start + step * i);
                    break;

                default:
                    yield return baseDirection;
                    break;
            }
        }

        private Vector2 GetBaseDirection(NodeContext ctx, Vector3 origin) {
            ShootAimSource aimSource = m_pattern == ShootPattern.AimedAtPlayer ? ShootAimSource.Player : m_aimSource;
            if (aimSource == ShootAimSource.PlayerSnapshot && TryResolvePlayerSnapshot(ctx, out Vector2 snapshotPosition)) {
                Vector2 snapshotDirection = snapshotPosition - (Vector2)origin;
                if (snapshotDirection.sqrMagnitude > 0.0001f)
                    return snapshotDirection.normalized;
            }

            if (TryResolveAimTarget(ctx, aimSource, out Transform target)) {
                Vector2 direction = target.position - origin;
                if (direction.sqrMagnitude > 0.0001f)
                    return direction.normalized;
            }

            Vector2 fixedDirection = m_direction.Evaluate();
            return fixedDirection.sqrMagnitude > 0.0001f ? fixedDirection.normalized : Vector2.left;
        }

        private bool TryResolveAimTarget(NodeContext ctx, ShootAimSource aimSource, out Transform target) {
            target = null;

            switch (aimSource) {
                case ShootAimSource.Player:
                    return ctx.blackboard.TryGet(BKeys.PlayerTransform, out target) && target != null;

                case ShootAimSource.Boss:
                    return ctx.blackboard.TryGet(BKeys.BossTransform, out target) && target != null;

                case ShootAimSource.TargetTransform:
                    target = m_targetTransform;
                    return target != null;
            }

            return false;
        }

        private bool TryResolvePlayerSnapshot(NodeContext ctx, out Vector2 snapshotPosition) {
            if (ctx.blackboard.TryGetValue(PlayerSnapshotKey, out snapshotPosition))
                return true;

            if (!ctx.blackboard.TryGet(BKeys.PlayerTransform, out Transform player) || player == null)
                return false;

            snapshotPosition = player.position;
            ctx.blackboard.SetValue(PlayerSnapshotKey, snapshotPosition);
            return true;
        }

        private void SpawnBullet(Vector3 origin, Vector2 direction, float speed) {
            GameObject bulletObject = Instantiate(m_bulletPrefab, origin, Quaternion.identity);
            bulletObject.SendMessage("SetOwnerTeam", m_ownerTeam, SendMessageOptions.DontRequireReceiver);
            bulletObject.SendMessage("SetRotation", direction, SendMessageOptions.DontRequireReceiver);

            if (bulletObject.TryGetComponent(out Rigidbody2D rb))
                rb.linearVelocity = direction.normalized * speed;
        }

        private Vector3 ResolvePosition(NodeContext ctx) {
            Vector3 resolved = m_spawnPosition.Evaluate();
            Vector2 offset = m_offset.Evaluate();

            switch (m_positionSource) {
                case SpawnPositionSource.Player:
                    if (ctx.blackboard.TryGet(BKeys.PlayerTransform, out Transform player) && player != null)
                        resolved = player.position;
                    break;

                case SpawnPositionSource.Boss:
                    if (ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) && boss != null)
                        resolved = boss.position;
                    break;

                case SpawnPositionSource.TargetPoint:
                    if (SpawnPointResolver.TryResolveByKey(ctx, m_spawnPointKey, out Vector2 keyedSpawnPosition))
                        resolved = keyedSpawnPosition;
                    break;
            }

            return resolved + (Vector3)offset;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees) {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos
            ).normalized;
        }
    }
}
