using UnityEngine;
using Utility;

namespace Game.Player {
    public class PlayerGroundCheck : MonoBehaviour {
        [SerializeField] private BoolSignalAsset m_isOnGround;
        [SerializeField] private LayerMask m_groundLayer = ~0;
        [SerializeField] private Transform m_groundCheck;
        [SerializeField] private float m_groundCheckRadius = 0.2f;
        private bool m_previousGroundedState;

        private void FixedUpdate() {
            CheckGrounded();
        }

        private void CheckGrounded() {
            if (m_groundCheck == null)
                return;

            LayerMask groundLayer = m_groundLayer.value == 0 ? ~0 : m_groundLayer;
            bool isGrounded = Physics2D.OverlapCircle(m_groundCheck.position, m_groundCheckRadius, groundLayer);
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