using UnityEngine;
using Utility;

namespace Game.Player {
    [RequireComponent(typeof(Rigidbody2D))]
    public class TopDownShipPlayer : MonoBehaviour {
        public enum FacingMode {
            Pointer,
            MovementDirection,
            None
        }

        [Header("Signals")]
        [SerializeField] private BoolSignalAsset m_isMoving;

        [Header("Movement")]
        [Range(1f, 30f)] [SerializeField] private float m_maxSpeed = 9f;
        [Range(1f, 120f)] [SerializeField] private float m_acceleration = 45f;
        [Range(1f, 120f)] [SerializeField] private float m_deceleration = 35f;
        [Range(0f, 10f)] [SerializeField] private float m_linearDamping = 1.5f;

        [Header("Facing")]
        [SerializeField] private FacingMode m_facingMode = FacingMode.Pointer;
        [Range(90f, 1440f)] [SerializeField] private float m_rotationSpeed = 720f;
        [SerializeField] private float m_spriteForwardAngleOffset = -90f;

        private Rigidbody2D m_rb;
        private Camera m_camera;
        private Vector2 m_moveInput;

        private void Awake() {
            m_rb = GetComponent<Rigidbody2D>();
            m_camera = Camera.main;

            m_rb.gravityScale = 0f;
            m_rb.linearDamping = m_linearDamping;
            m_rb.angularDamping = 8f;
            m_rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
        }

        private void Update() {
            m_moveInput = InputManager.instance.GetMovementVectorRaw();
            if (m_moveInput.sqrMagnitude > 1f) {
                m_moveInput.Normalize();
            }

            m_isMoving?.Invoke(m_moveInput.sqrMagnitude > 0.01f);
        }

        private void FixedUpdate() {
            MoveShip();
            RotateShip();
        }

        private void MoveShip() {
            Vector2 desiredVelocity = m_moveInput * m_maxSpeed;
            float rate = m_moveInput.sqrMagnitude > 0.01f ? m_acceleration : m_deceleration;
            Vector2 velocity = Vector2.MoveTowards(
                m_rb.linearVelocity,
                desiredVelocity,
                rate * Time.fixedDeltaTime
            );

            m_rb.linearVelocity = Vector2.ClampMagnitude(velocity, m_maxSpeed);
        }

        private void RotateShip() {
            if (!TryGetFacingDirection(out Vector2 direction)) return;

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + m_spriteForwardAngleOffset;
            float angle = Mathf.MoveTowardsAngle(
                m_rb.rotation,
                targetAngle,
                m_rotationSpeed * Time.fixedDeltaTime
            );

            m_rb.MoveRotation(angle);
        }

        private bool TryGetFacingDirection(out Vector2 direction) {
            direction = Vector2.zero;

            switch (m_facingMode) {
                case FacingMode.Pointer:
                    if (m_camera == null) {
                        m_camera = Camera.main;
                    }

                    if (m_camera == null) return false;

                    Vector3 pointerWorldPosition = m_camera.ScreenToWorldPoint(InputManager.instance.GetPointerPosition());
                    direction = pointerWorldPosition - transform.position;
                    break;
                case FacingMode.MovementDirection:
                    direction = m_rb.linearVelocity.sqrMagnitude > 0.04f ? m_rb.linearVelocity : m_moveInput;
                    break;
                case FacingMode.None:
                default:
                    return false;
            }

            if (direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            return true;
        }
    }
}
