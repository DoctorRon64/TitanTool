using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace Game {
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour, IPoolable {
        [SerializeField] private int m_damageValue = 1;
        [SerializeField] private DamagableTeam m_ownerTeam = DamagableTeam.Opponent;
        [SerializeField] private LayerMask m_enviourmentMask;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private Animator m_anim;
        [SerializeField] private float m_crossFadeAnim = 0.5f;
        [FormerlySerializedAs("idleState")]
        [SerializeField] private string m_idleState = "bullet_idle";
        [FormerlySerializedAs("explodeState")]
        [SerializeField] private string m_explodeState = "bullet_explode";

        public bool active { get; set; }
        private Rigidbody2D m_rb;
        private ObjectPool<Bullet> m_objectPool;
        private bool m_isDestroying;

        public bool isDestroying => m_isDestroying;

        private void Awake() {
            m_rb = GetComponent<Rigidbody2D>();
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

                damagable.TakeDamage(m_damageValue);
                DestroyBullet();
                return;
            }

            bool hitEnvironment = (m_enviourmentMask.value & (1 << _other.gameObject.layer)) != 0;
            if (hitEnvironment) {
                DestroyBullet();
            }
        }

        private IEnumerator WaitUntilAnimationFinished() {
            if (m_anim == null || m_anim.runtimeAnimatorController == null) {
                CleanupBullet();
                yield break;
            }

            yield return null;
            while (m_anim != null && m_anim.runtimeAnimatorController != null) {
                AnimatorStateInfo state = m_anim.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName(m_explodeState) || state.normalizedTime >= 1f)
                    break;
                yield return null;
            }

            CleanupBullet();
        }

        public void EnablePoolable() {
            m_isDestroying = false;
            gameObject.SetActive(true);
            if (m_spriteRenderer != null)
                m_spriteRenderer.enabled = true;
            if (m_anim == null || m_anim.runtimeAnimatorController == null) return;
            m_anim.enabled = true;
            m_anim.Rebind();
            m_anim.Update(0f);
            if (m_anim.HasState(0, Animator.StringToHash(m_idleState)))
                m_anim.Play(m_idleState, 0, 0f);
        }

        public void DisablePoolable() {
            StopAllCoroutines();
            m_isDestroying = false;
            m_rb.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);
            if (m_spriteRenderer != null)
                m_spriteRenderer.enabled = true;
        }

        public void SetPosition(Vector2 _position) {
            transform.position = _position;
        }

        private void DestroyBullet() {
            if (m_isDestroying) return;
            m_isDestroying = true;
            m_rb.linearVelocity = Vector2.zero;
            if (m_anim == null || m_anim.runtimeAnimatorController == null) {
                CleanupBullet();
                return;
            }

            int explodeStateHash = Animator.StringToHash(m_explodeState);
            if (!m_anim.HasState(0, explodeStateHash)) {
                CleanupBullet();
                return;
            }

            m_anim.CrossFade(explodeStateHash, m_crossFadeAnim, 0);
            StartCoroutine(WaitUntilAnimationFinished());
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
