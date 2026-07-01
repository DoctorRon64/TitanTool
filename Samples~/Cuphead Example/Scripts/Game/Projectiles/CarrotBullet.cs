using UnityEngine;
using Utility;

namespace Game {
    [RequireComponent(typeof(Bullet))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class CarrotBullet : MonoBehaviour, IDamagable {
        [SerializeField] private int m_maxHealth = 3;
        [SerializeField] private float m_speed = 3f;
        [SerializeField] private float m_turnSpeed = 90f;
        [SerializeField] private DamagableTeam m_team = DamagableTeam.Opponent;

        private Bullet m_bullet;
        private Rigidbody2D m_rb;
        private Transform m_target;
        private float m_currentSpeed;
        private bool m_capturedSpawnSpeed;

        public DamagableTeam team => m_team;
        public int health { get; set; }

        private void Awake() {
            m_bullet = GetComponent<Bullet>();
            m_rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable() {
            health = m_maxHealth;
            m_target = null;
            m_currentSpeed = Mathf.Max(0f, m_speed);
            m_capturedSpawnSpeed = false;
        }

        private void FixedUpdate() {
            if (m_bullet.isDestroying)
                return;

            CaptureSpawnSpeed();
            ResolveTarget();

            if (m_target == null)
                return;

            Vector2 toTarget = m_target.position - transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Vector2 currentDirection = m_rb.linearVelocity.sqrMagnitude > 0.0001f
                ? m_rb.linearVelocity.normalized
                : (Vector2)transform.right;

            Vector2 newDirection = Vector3.RotateTowards(
                currentDirection,
                toTarget.normalized,
                m_turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
                0f
            );

            m_rb.linearVelocity = newDirection.normalized * m_currentSpeed;
            SetRotation(newDirection);
        }

        public void TakeDamage(int damageAmount) {
            if (m_bullet.isDestroying)
                return;

            health -= damageAmount;
            if (health <= 0)
                m_bullet.BreakBullet();
        }

        public void SetOwnerTeam(DamagableTeam team) {
            m_team = team;
        }

        private void CaptureSpawnSpeed() {
            if (m_capturedSpawnSpeed)
                return;

            m_capturedSpawnSpeed = true;
            float spawnedSpeed = m_rb.linearVelocity.magnitude;
            if (spawnedSpeed > 0.01f)
                m_currentSpeed = spawnedSpeed;
        }

        private void ResolveTarget() {
            if (m_target != null)
                return;

            Player.Player player = FindFirstObjectByType<Player.Player>();
            if (player != null)
                m_target = player.transform;
        }

        private void SetRotation(Vector2 direction) {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
