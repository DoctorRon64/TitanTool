using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utility;

[RequireComponent(typeof(Rigidbody2D))]
public class Bomb : MonoBehaviour {
    [SerializeField] private int m_damageValue = 1;
    [SerializeField] private LayerMask m_enviourmentMask;
    [SerializeField] private LayerMask m_damageMask = ~0;
    [SerializeField] private float m_explosionRadius = 2f;
    [SerializeField] private float m_lifeTime = 5f;
    [SerializeField] private DamagableTeam m_ownerTeam;
    [SerializeField] private Animator m_animator;
    [SerializeField] private float m_animTransition = 0.5f;
    [SerializeField] private string m_idleAnim = "Idle";
    [SerializeField] private string m_explodeAnim = "Explode";

    private Rigidbody2D m_rb;
    private bool m_isDestroying;
    private float m_lifeTimer;

    private void Awake() {
        m_rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable() {
        m_isDestroying = false;
        m_lifeTimer = 0f;
        PlayAnimation(m_idleAnim, 0f);
    }

    private void Update() {
        if (m_isDestroying)
            return;

        m_lifeTimer += Time.deltaTime;
        if (m_lifeTimer >= m_lifeTime)
            Explode();
    }

    public void SetDirection(Vector2 direction, float speed) {
        m_rb.linearVelocity = direction.normalized * speed;
    }

    public void SetOwnerTeam(DamagableTeam team) {
        m_ownerTeam = team;
    }

    public void SetRotation(Vector2 direction) {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void OnTriggerEnter2D(Collider2D _other) {
        if (m_isDestroying) return;

        if (_other.TryGetComponent(out IDamagable damagable)) {
            if (damagable.team == m_ownerTeam)
                return;

            if (damagable is Game.Bullet bullet && !bullet.canBeShotDown)
                return;

            Explode();
            return;
        }

        bool hitEnvironment = (m_enviourmentMask.value & (1 << _other.gameObject.layer)) != 0;
        if (hitEnvironment) {
            Explode();
        }
    }

    public void Explode() {
        if (m_isDestroying)
            return;

        m_isDestroying = true;
        m_rb.linearVelocity = Vector2.zero;

        DamageTargetsInRadius();

        if (!PlayAnimation(m_explodeAnim, m_animTransition)) {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(WaitForExplosionAnimation());
    }

    private IEnumerator WaitForExplosionAnimation() {
        yield return null;

        while (HasUsableAnimator(m_animator)) {
            AnimatorStateInfo state = m_animator.GetCurrentAnimatorStateInfo(0);

            if (state.IsName(m_explodeAnim) && state.normalizedTime >= 1f && !m_animator.IsInTransition(0))
                break;

            yield return null;
        }

        Destroy(gameObject);
    }

    private bool PlayAnimation(string stateName, float transitionDuration) {
        if (!HasUsableAnimator(m_animator))
            return false;

        int stateHash = Animator.StringToHash(stateName);
        if (!m_animator.HasState(0, stateHash))
            return false;

        if (transitionDuration > 0f)
            m_animator.CrossFade(stateHash, transitionDuration, 0);
        else
            m_animator.Play(stateHash, 0, 0f);

        return true;
    }

    private static bool HasUsableAnimator(Animator animator) {
        return animator != null && animator.runtimeAnimatorController != null;
    }

    private void DamageTargetsInRadius() {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, m_explosionRadius, m_damageMask);
        HashSet<IDamagable> damagedTargets = new();

        foreach (Collider2D hit in hits) {
            if (!hit.TryGetComponent(out IDamagable damagable))
                continue;

            if (damagable.team == m_ownerTeam)
                continue;

            if (!damagedTargets.Add(damagable))
                continue;

            damagable.TakeDamage(m_damageValue);
        }
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_explosionRadius);
    }
}
