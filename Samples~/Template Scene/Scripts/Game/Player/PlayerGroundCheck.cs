using UnityEngine;
using Utility;

namespace Game.Player {
    public class PlayerGroundCheck : MonoBehaviour {
        [SerializeField] private BoolSignalAsset m_isOnGround;
        [SerializeField] private LayerMask m_groundLayer;
        [SerializeField] private Transform m_groundCheck;
        [SerializeField] private float m_groundCheckRadius = 0.2f;
        private bool m_previousGroundedState;

        private void FixedUpdate() {
            CheckGrounded();
        }

        private void CheckGrounded() {
            bool isGrounded = Physics2D.OverlapCircle(m_groundCheck.position, m_groundCheckRadius, m_groundLayer);
            if (isGrounded == m_previousGroundedState) return;
            m_previousGroundedState = isGrounded;
            m_isOnGround?.Invoke(isGrounded);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            if (!m_groundCheck) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                m_groundCheck.position,
                m_groundCheckRadius
            );
        }
#endif
    }
}