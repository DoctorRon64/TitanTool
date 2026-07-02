using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace Game {
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour, IPoolable, IDamagable {
        [SerializeField] private int m_damageValue = 1;
        [SerializeField] private DamagableTeam m_ownerTeam = DamagableTeam.Opponent;
        [FormerlySerializedAs("m_canTakeDamage")]
        [SerializeField] private bool m_canBeShotDown;
        [SerializeField] private int m_maxHealth = 1;
        [SerializeField] private LayerMask m_enviourmentMask;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private Animator m_anim;
        [SerializeField] private float m_crossFadeAnim = 0.25f;
        [FormerlySerializedAs("idleState")]
        [SerializeField] private string m_idleState = "bullet_idle";
        [FormerlySerializedAs("explodeState")]
        [SerializeField] private string m_explodeState = "bullet_explode";
        [SerializeField] private bool m_useHoming;
        [SerializeField] private float m_homingTurnSpeed = 120f;
        [SerializeField] private bool m_findPlayerAsHomingTarget = true;

        public DamagableTeam team => m_ownerTeam;
        public bool canBeShotDown => m_canBeShotDown;
        public int health { get; set; }
        public bool active { get; set; }
        private Rigidbody2D m_rb;
        private ObjectPool<Bullet> m_objectPool;
        private bool m_isDestroying;
        private Transform m_resolvedHomingTarget;
        private float m_homingSpeed;
        private bool m_capturedHomingSpeed;

        public bool isDestroying => m_isDestroying;

        private void Awake() {
            m_rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable() {
            ResetHoming();
            ResetHealth();
        }

        private void FixedUpdate() {
            UpdateHoming();
        }

        public void SetupBullet(ObjectPool<Bullet> _pool) {
            m_objectPool = _pool;
        }

        public void SetDirection(Vector2 _direction, float _speed) {
            m_rb.linearVelocity = _direction.normalized * _speed;
        }

        public void SetOwnerTeam(DamagableTeam _team) {
            m_ownerTeam = _team;
        }

        public void SetRotation(Vector2 _direction) {
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void OnTriggerEnter2D(Collider2D _other) {
            if (m_isDestroying) return;

            if (_other.TryGetComponent(out IDamagable damagable)) {
                if (damagable.team == m_ownerTeam)
                    return;

                if (damagable is Bullet bullet && !bullet.canBeShotDown)
                    return;

                damagable.TakeDamage(m_damageValue);
                DestroyBullet();
                return;
            }

            bool hitEnvironment = (m_enviourmentMask.value & (1 << _other.gameObject.layer)) != 0;
            if (hitEnvironment) {
                DestroyBullet();
            }
        }

        private IEnumerator WaitUntilAnimationFinished(Animator animator) {
            if (!HasUsableAnimator(animator)) {
                CleanupBullet();
                yield break;
            }

            yield return null;

            while (HasUsableAnimator(animator)) {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName(m_explodeState) && state.normalizedTime >= 1f && !animator.IsInTransition(0))
                    break;

                yield return null;
            }

            CleanupBullet();
        }

        public void EnablePoolable() {
            m_isDestroying = false;
            ResetHoming();
            ResetHealth();
            gameObject.SetActive(true);
            if (m_spriteRenderer != null)
                m_spriteRenderer.enabled = true;

            Animator animator = ResolveAnimator();
            if (!HasUsableAnimator(animator)) return;

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);

            int idleStateHash = Animator.StringToHash(m_idleState);
            if (animator.HasState(0, idleStateHash))
                animator.Play(idleStateHash, 0, 0f);
        }

        public void DisablePoolable() {
            StopAllCoroutines();
            m_isDestroying = false;
            ResetHoming();
            m_rb.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);
            if (m_spriteRenderer != null)
                m_spriteRenderer.enabled = true;
        }

        public void TakeDamage(int damageAmount) {
            if (!m_canBeShotDown || m_isDestroying)
                return;

            health -= damageAmount;
            if (health <= 0)
                BreakBullet();
        }

        public void SetPosition(Vector2 _position) {
            transform.position = _position;
        }

        private void DestroyBullet() {
            if (m_isDestroying) return;
            m_isDestroying = true;
            m_rb.linearVelocity = Vector2.zero;

            Animator animator = ResolveAnimator();
            if (!HasUsableAnimator(animator)) {
                CleanupBullet();
                return;
            }

            int explodeStateHash = Animator.StringToHash(m_explodeState);
            if (!animator.HasState(0, explodeStateHash)) {
                CleanupBullet();
                return;
            }

            animator.CrossFade(explodeStateHash, m_crossFadeAnim, 0);
            StartCoroutine(WaitUntilAnimationFinished(animator));
        }

        private Animator ResolveAnimator() {
            if (HasUsableAnimator(m_anim))
                return m_anim;

            foreach (Animator animator in GetComponentsInChildren<Animator>(true)) {
                if (!HasUsableAnimator(animator))
                    continue;

                m_anim = animator;
                return m_anim;
            }

            return m_anim;
        }

        private void UpdateHoming() {
            if (!m_useHoming || m_isDestroying)
                return;

            CaptureHomingSpeed();
            ResolveHomingTarget();

            if (m_resolvedHomingTarget == null)
                return;

            Vector2 toTarget = m_resolvedHomingTarget.position - transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Vector2 currentDirection = m_rb.linearVelocity.sqrMagnitude > 0.0001f
                ? m_rb.linearVelocity.normalized
                : (Vector2)transform.right;

            Vector2 newDirection = Vector3.RotateTowards(
                currentDirection,
                toTarget.normalized,
                m_homingTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
                0f
            );

            m_rb.linearVelocity = newDirection.normalized * m_homingSpeed;
            SetRotation(newDirection);
        }

        private void CaptureHomingSpeed() {
            if (m_capturedHomingSpeed)
                return;

            m_capturedHomingSpeed = true;
            m_homingSpeed = m_rb.linearVelocity.magnitude;
        }

        private void ResolveHomingTarget() {
            if (m_resolvedHomingTarget != null || !m_findPlayerAsHomingTarget)
                return;

            Player.Player player = FindFirstObjectByType<Player.Player>();
            if (player != null)
                m_resolvedHomingTarget = player.transform;
        }

        private void ResetHoming() {
            m_resolvedHomingTarget = null;
            m_homingSpeed = 0f;
            m_capturedHomingSpeed = false;
        }

        private void ResetHealth() {
            health = m_maxHealth;
        }

        private static bool HasUsableAnimator(Animator animator) {
            return animator != null && animator.runtimeAnimatorController != null;
        }

        private void CleanupBullet() {
            if (m_objectPool != null) {
                m_objectPool.DeactivateItem(this);
                return;
            }

            Destroy(gameObject);
        }

        public void BreakBullet() {
            DestroyBullet();
        }
    }
}
